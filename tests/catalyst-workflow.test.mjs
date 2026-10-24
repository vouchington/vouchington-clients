import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'
import { jobBlock } from './workflow-job-block.mjs'

const workflow = await readFile(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)
const rendered = jobBlock(workflow, 'dotnet-maui-tests')
const app = jobBlock(workflow, 'dotnet-maui')
const step = (job, name) => {
  const rest = job.split(`      - name: ${name}\n`)[1]
  assert.ok(rest, name)
  return rest.split('      - name: ')[0]
}

test('rendered pages and full Catalyst build run independently and both remain required', () => {
  for (const job of [rendered, app]) {
    assert.match(job, /needs: verify\n/u)
    assert.match(job, /runs-on: macos-latest/u)
    assert.doesNotMatch(job, /matrix:|needs: \[|^    continue-on-error:/mu)
  }
  assert.match(rendered, /timeout-minutes: 10\n/u)
  assert.match(app, /timeout-minutes: 30\n/u)
  const aggregate = jobBlock(workflow, 'tests')
  assert.match(aggregate, /- dotnet-maui-tests\n\s+- dotnet-maui\n/u)
  assert.match(aggregate, /- dotnet-catalyst-gui/u)
  for (const job of [rendered, app]) {
    assert.doesNotMatch(
      job,
      /CoreBuild|compile-state|source-state|materialize-sdk-version|consumer-tooling-root/u,
    )
  }
})

test('the rendered shard preserves the complete test suite, locked dependencies and failure diagnostics', () => {
  assert.ok(
    step(rendered, 'Verify all twelve committed NuGet locks').includes('restore-locks.sh verify'),
  )
  const build = step(rendered, 'Build rendered MAUI page tests')
  assert.ok(
    build.includes(
      'dotnet build dotnet-clients/tests/Voucha.Client.App.Tests/Voucha.Client.App.Tests.csproj',
    ),
  )
  assert.match(build, /--configuration Release --no-restore/u)
  const tests = step(rendered, 'Test rendered MAUI pages')
  assert.ok(
    tests.includes(
      'dotnet test dotnet-clients/tests/Voucha.Client.App.Tests/Voucha.Client.App.Tests.csproj',
    ),
  )
  assert.match(tests, /--configuration Release --no-build/u)
  assert.match(tests, /--blame-hang --blame-hang-timeout 2m --blame-hang-dump-type mini/u)
  assert.doesNotMatch(tests, /--filter|continue-on-error|\|\| true/u)
  const diagnostics = step(rendered, 'Upload failed MAUI test diagnostics')
  assert.ok(diagnostics.includes('if: ${{ failure() || cancelled() }}'))
  assert.doesNotMatch(app, /Build rendered MAUI page tests|Test rendered MAUI pages/u)
})

test('the app shard retains the original full Release Build, pinned tools and locked project graph', () => {
  assert.ok(
    step(app, 'Verify all twelve committed NuGet locks').includes('restore-locks.sh verify'),
  )
  assert.match(step(app, 'Select compatible Xcode'), /select-xcode\.sh 26\.6/u)
  assert.match(
    step(app, 'Detect host Mac Catalyst RID'),
    /maccatalyst-arm64[\s\S]*maccatalyst-x64/u,
  )
  const graph = step(app, 'Restore the shared MAUI project-reference graph')
  assert.ok(graph.includes('dotnet-clients/src/Voucha.Client.App/Voucha.Client.App.csproj'))
  assert.ok(graph.includes('dotnet-clients/src/Voucha.Client.Core/Voucha.Client.Core.csproj'))
  assert.equal(graph.match(/--locked-mode/gu)?.length, 2)
  const build = step(app, 'Build MAUI Mac Catalyst app')
  assert.ok(
    build.includes('dotnet build dotnet-clients/src/Voucha.Client.App/Voucha.Client.App.csproj'),
  )
  assert.match(build, /--configuration Release --framework net10\.0-maccatalyst/u)
  assert.ok(build.includes('-p:VouchaTurnstileSiteKey=0x4AAAAAAAAexample'))
  assert.ok(build.includes('"-p:RuntimeIdentifier=${{ steps.rid.outputs.runtime_identifier }}"'))
  assert.match(build, /--no-restore -clp:PerformanceSummary/u)
  assert.doesNotMatch(build, /-t:|Skip|BuildProjectReferences=false|continue-on-error|\|\| true/u)
})

test('each shard independently verifies its candidate and tools, with NuGet writes restricted to main app success', () => {
  for (const job of [rendered, app]) {
    assert.ok(job.includes('uses: ./.github/actions/prepare-native-contract'))
    assert.ok(job.includes('candidate-revision-sha: ${{ needs.verify.outputs.revision-sha }}'))
    assert.ok(job.includes('producer-run-attempt: ${{ github.run_attempt }}'))
    assert.ok(job.includes('producer-run-id: ${{ github.run_id }}'))
    assert.ok(job.includes('uses: ./.github/actions/setup-mise-toolchain'))
    assert.ok(job.includes('client-root: candidate-clients'))
    assert.ok(job.includes('tool: dotnet'))
    assert.match(job, /dotnet workload install maui-maccatalyst/u)
    assert.match(job, /--configfile dotnet-clients\/NuGet\.config/u)
    assert.doesNotMatch(job, /consumer-tooling\/|compile-state|source-state/u)
  }
  assert.doesNotMatch(rendered, /actions\/cache\/save@/u)
  const save = step(app, 'Save NuGet packages from main')
  assert.ok(save.includes("github.event_name == 'push' && github.ref == 'refs/heads/main'"))
  assert.ok(save.includes("steps.maui-nuget-cache.outputs.cache-hit != 'true'"))
})
