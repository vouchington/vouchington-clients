import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { it } from 'node:test'

const readWorkflow = () =>
  readFile(new URL('../.github/workflows/native-contract-tests.yml', import.meta.url), 'utf8')
const readAction = () =>
  readFile(
    new URL('../.github/actions/prepare-native-contract/action.yml', import.meta.url),
    'utf8',
  )
const jobBlock = (workflow, job) => {
  const start = workflow.indexOf(`  ${job}:`)
  assert.notEqual(start, -1, `missing job ${job}`)
  const remainder = workflow.slice(start)
  const nextJob = remainder.search(/\n {2}[A-Za-z0-9_-]+:/u)
  return remainder.slice(0, nextJob === -1 ? remainder.length : nextJob)
}
const stepBlock = (job, step) => {
  const start = job.indexOf(`      - name: ${step}\n`)
  assert.notEqual(start, -1, `missing step ${step}`)
  const remainder = job.slice(start)
  const nextStep = remainder.slice(1).search(/\n {6}- name:/u)
  return remainder.slice(0, nextStep === -1 ? remainder.length : nextStep + 1)
}

it('uses the same explicit Release Turnstile key in both Catalyst build phases', async () => {
  const workflow = await readWorkflow()
  for (const [job, step] of [
    ['dotnet-maui-compile', 'Build managed Mac Catalyst CoreBuild closure'],
    ['dotnet-maui', 'Build MAUI Mac Catalyst app'],
  ]) {
    const build = stepBlock(jobBlock(workflow, job), step)
    assert.match(
      build,
      /dotnet build dotnet-clients\/src\/Voucha\.Client\.App\/Voucha\.Client\.App\.csproj/u,
    )
    assert.deepEqual(
      [...build.matchAll(/-p:VouchaTurnstileSiteKey=([^\s]+)/gu)].map(match => match[1]),
      ['0x4AAAAAAAAexample'],
      `${job} must supply the existing public CI key to its Release build`,
    )
  }
})

it('runs portable .NET tests once on Linux and MAUI tests on macOS', async () => {
  const [action, workflow] = await Promise.all([readAction(), readWorkflow()])
  const portable = jobBlock(workflow, 'dotnet-portable')
  const mauiCompile = jobBlock(workflow, 'dotnet-maui-compile')
  const maui = jobBlock(workflow, 'dotnet-maui')
  const windows = jobBlock(workflow, 'dotnet-windows-image-preview')

  assert.match(portable, /^    name: \.NET portable$/mu)
  assert.match(portable, /runs-on: ubuntu-latest/u)
  assert.doesNotMatch(portable, /matrix:/u)
  assert.doesNotMatch(portable, /macos/u)
  assert.doesNotMatch(portable, /matrix\.os/u)
  assert.match(maui, /runs-on: macos-latest/u)
  assert.match(mauiCompile, /runs-on: macos-latest/u)
  assert.equal(workflow.split('name: Materialize verified SDK archive metadata').length - 1, 2)
  for (const catalystJob of [mauiCompile, maui]) {
    assert.match(
      catalystJob,
      /name: Set up mise-managed \.NET SDK[\s\S]*name: Materialize verified SDK archive metadata\n\s+timeout-minutes: 7\n\s+working-directory: candidate-clients\n\s+run: \|\n\s+node dotnet-clients\/tooling\/materialize-sdk-version-metadata\.mjs "\$DOTNET_ROOT"\n\s+test "\$\(dotnet --version\)" = "\$\(jq -r '\.sdk\.version' global\.json\)"[\s\S]*name: Detect pinned MAUI workload/u,
    )
  }
  assert.equal(windows.split('-p:MSBuildEnableWorkloadResolver=false').length - 1, 2)
  assert.equal(workflow.split('-p:MSBuildEnableWorkloadResolver=false').length - 1, 4)
  assert.match(workflow, /dotnet test Voucha\.DotNet\.sln[\s\S]*XPlat Code Coverage/u)
  assert.match(workflow, /coverage\.info[\s\S]*TestResults\/core\/lcov\.info/u)
  assert.match(workflow, /run: pnpm run coverage:dotnet-core/u)
  for (const command of [
    /restore-locks\.sh verify/u,
    /dotnet build dotnet-clients\/tests\/Voucha\.Client\.App\.Tests\/Voucha\.Client\.App\.Tests\.csproj/u,
    /dotnet test dotnet-clients\/tests\/Voucha\.Client\.App\.Tests\/Voucha\.Client\.App\.Tests\.csproj/u,
    /dotnet build dotnet-clients\/src\/Voucha\.Client\.App\/Voucha\.Client\.App\.csproj[\s\S]*--framework net10\.0-maccatalyst/u,
  ])
    assert.match(workflow, command)
  assert.match(maui, /run: bash dotnet-clients\/tooling\/select-xcode\.sh 26\.6/u)
  assert.doesNotMatch(maui, /compatible=false|steps\.xcode\.outputs\.compatible/u)
  assert.match(maui, /name: Restore the shared MAUI project-reference graph[\s\S]*--locked-mode/u)
  assert.match(maui, /name: Build MAUI Mac Catalyst app/u)
  assert.match(
    maui,
    /name: Build MAUI Mac Catalyst app[\s\S]*python3 dotnet-clients\/tooling\/catalyst-msbuild-reasons\.py --root \. --[\s\S]*dotnet build dotnet-clients\/src\/Voucha\.Client\.App\/Voucha\.Client\.App\.csproj[\s\S]*--no-restore -clp:PerformanceSummary/u,
  )
  assert.match(mauiCompile, /name: Test rendered MAUI pages/u)
  assert.match(mauiCompile, /name: Verify all twelve committed NuGet locks/u)
  assert.match(maui, /name: Verify all twelve committed NuGet locks/u)
  assert.match(
    mauiCompile,
    /name: Build managed Mac Catalyst CoreBuild closure[\s\S]*--no-restore -t:CoreBuild/u,
  )
  assert.match(mauiCompile, /name: Upload exact managed compile state/u)
  assert.match(mauiCompile, /source-state\.tar\.gz/u)
  assert.match(mauiCompile, /name: Pack exact managed compile state\n\s+timeout-minutes: 2/u)
  assert.match(maui, /needs: \[verify, dotnet-maui-compile\]/u)
  assert.match(jobBlock(workflow, 'tests'), /- dotnet-maui-compile\n\s+- dotnet-maui/u)
  assert.match(
    maui,
    /name: Verify and restore exact managed compile state[\s\S]*name: Build MAUI Mac Catalyst app/u,
  )
  assert.match(maui, /name: Verify and restore exact managed compile state\n\s+timeout-minutes: 2/u)
  assert.doesNotMatch(maui, /name: Test rendered MAUI pages/u)
  assert.match(
    maui,
    /dotnet restore dotnet-clients\/src\/Voucha\.Client\.App\/Voucha\.Client\.App\.csproj \\\n\s+-p:Configuration=Release -p:TargetFramework=net10\.0-maccatalyst \\\n\s+-p:RuntimeIdentifier=\$\{\{ steps\.rid\.outputs\.runtime_identifier \}\} --locked-mode\n\s+dotnet restore dotnet-clients\/src\/Voucha\.Client\.Core\/Voucha\.Client\.Core\.csproj \\\n\s+-p:Configuration=Release -p:TargetFramework=net10\.0 --locked-mode/u,
  )
  assert.ok(
    workflow.indexOf('name: Test rendered MAUI pages') <
      workflow.indexOf('name: Select compatible Xcode'),
  )
  assert.doesNotMatch(workflow, /clean-workspace/u)
  assert.doesNotMatch(action, /clean-workspace/u)
})

it('keeps Catalyst tooling checkout outside the candidate compiler input ancestry', async () => {
  const [action, workflow] = await Promise.all([readAction(), readWorkflow()])
  for (const job of ['dotnet-maui-compile', 'dotnet-maui']) {
    const catalyst = jobBlock(workflow, job)
    assert.match(
      catalyst,
      /name: Check out consumer tooling\n\s+uses: actions\/checkout@[^\n]+\n\s+with:\n\s+persist-credentials: false\n\s+path: consumer-tooling/u,
    )
    assert.match(catalyst, /node-version-file: consumer-tooling\/\.nvmrc/u)
    assert.match(
      catalyst,
      /uses: \.\/consumer-tooling\/\.github\/actions\/prepare-native-contract\n\s+with:\n\s+candidate-revision-sha: [^\n]+\n\s+consumer-tooling-root: consumer-tooling/u,
    )
    assert.match(catalyst, /uses: \.\/consumer-tooling\/\.github\/actions\/setup-mise-toolchain/u)
    assert.match(catalyst, /client-root: candidate-clients/u)
    assert.match(catalyst, /working-directory: candidate-clients/u)
    assert.doesNotMatch(catalyst, /path: candidate-clients\/consumer-tooling/u)
  }
  assert.equal(workflow.match(/consumer-tooling-root: consumer-tooling/gu)?.length, 2)
  assert.match(
    action,
    /consumer-tooling-root:\n\s+description: [^\n]+\n\s+required: false\n\s+default: '\.'/u,
  )
  assert.match(action, /node-version-file: \$\{\{ inputs\.consumer-tooling-root \}\}\/\.nvmrc/u)
  assert.match(action, /CONSUMER_TOOLING_ROOT: \$\{\{ inputs\.consumer-tooling-root \}\}/u)
  assert.match(action, /node "\$CONSUMER_TOOLING_ROOT\/scripts\/contract-artifact\.mjs" verify/u)
  assert.match(action, /path: candidate-clients\n\s+persist-credentials: false/u)
  assert.match(action, /working-directory: candidate-clients/u)
})
