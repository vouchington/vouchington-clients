import { resolve } from 'node:path'
import { matchesAnyChangedPath, runChangedFileRoute } from './changed-file-route.mjs'

const windowsMauiPaths = [
  '.editorconfig',
  '.github/actions/prepare-native-contract/action.yml',
  '.github/actions/setup-mise-toolchain/action.yml',
  '.github/workflows/native-contract-tests.yml',
  '.mise.toml',
  'contracts/filaments.json',
  'docs/development/ci-runners.md',
  'docs/development/native-ci-test-placement.md',
  'global.json',
  'dotnet-clients/.editorconfig',
  'dotnet-clients/AGENTS.md',
  'dotnet-clients/Directory.Build.props',
  'dotnet-clients/Directory.Build.targets',
  'dotnet-clients/Directory.Packages.props',
  'dotnet-clients/NuGet.config',
  'dotnet-clients/src/',
  'dotnet-clients/tooling/restore-locks.sh',
  'scripts/changed-file-route.mjs',
  'scripts/windows-maui-test-routing.mjs',
]

export function shouldRunWindowsMauiBuild(paths) {
  return matchesAnyChangedPath(paths, windowsMauiPaths)
}

function main() {
  runChangedFileRoute({
    repositoryRoot: resolve(import.meta.dirname, '..'),
    select: shouldRunWindowsMauiBuild,
    logLabel: 'Windows MAUI build changed-file route',
    outputKey: 'windows-maui',
  })
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) main()
