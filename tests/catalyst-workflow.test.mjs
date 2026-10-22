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
