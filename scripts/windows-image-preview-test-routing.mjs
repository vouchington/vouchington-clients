import { resolve } from 'node:path'
import { matchesAnyChangedPath, runChangedFileRoute } from './changed-file-route.mjs'

const windowsImagePreviewPaths = [
  '.editorconfig',
  '.github/actions/prepare-native-contract/action.yml',
  '.github/actions/setup-mise-toolchain/action.yml',
  '.github/workflows/native-contract-tests.yml',
  '.mise.toml',
  'docs/development/ci-runners.md',
  'docs/development/native-ci-test-placement.md',
  'dotnet-clients/AGENTS.md',
  'dotnet-clients/.editorconfig',
  'dotnet-clients/Directory.Build.props',
  'dotnet-clients/Directory.Build.targets',
  'dotnet-clients/Directory.Packages.props',
  'dotnet-clients/NuGet.config',
  'dotnet-clients/src/Voucha.Client.App/Support/LocalImagePreview.cs',
  'dotnet-clients/src/Voucha.Client.App/Support/WindowsLocalImagePreviewDecoder.cs',
  'dotnet-clients/tests/Voucha.Client.WindowsImagePreview.Tests/',
  'dotnet-clients/tooling/restore-locks.sh',
  'global.json',
  'scripts/changed-file-route.mjs',
  'scripts/windows-image-preview-test-routing.mjs',
  'tests/runner-policy.test.mjs',
  'tests/windows-image-preview-test-routing.test.mjs',
]

export function shouldRunWindowsImagePreviewTests(paths) {
  return matchesAnyChangedPath(paths, windowsImagePreviewPaths)
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) {
  runChangedFileRoute({
    repositoryRoot: resolve(import.meta.dirname, '..'),
    select: shouldRunWindowsImagePreviewTests,
    logLabel: 'Windows image preview changed-file route',
    outputKey: 'windows-image-preview',
  })
}
