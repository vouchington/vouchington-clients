import { resolve } from 'node:path'
import { matchesAnyChangedPath, runChangedFileRoute } from './changed-file-route.mjs'

const windowsLanguageModelSurfacePaths = [
  '.editorconfig',
  '.github/actions/prepare-native-contract/action.yml',
  '.github/actions/setup-mise-toolchain/action.yml',
  '.github/workflows/native-contract-tests.yml',
  '.github/workflows/repair-dependabot-dotnet-locks.yml',
  '.mise.toml',
  '.no-mistakes.yml',
  'docs/development/ci-runners.md',
  'docs/development/native-ci-test-placement.md',
  'dotnet-clients/.editorconfig',
  'dotnet-clients/AGENTS.md',
  'dotnet-clients/Directory.Build.props',
  'dotnet-clients/Directory.Build.targets',
  'dotnet-clients/Directory.Packages.props',
  'dotnet-clients/NuGet.config',
  'dotnet-clients/src/Voucha.Client.App/Chat/WindowsSystemLanguageModelRuntime.Windows.cs',
  'dotnet-clients/src/Voucha.Client.Core/Chat/WindowsSystemLanguageModelProvider.cs',
  'dotnet-clients/tests/Voucha.Client.WindowsLanguageModelSurface.Tests/',
  'dotnet-clients/tooling/restore-locks.sh',
  'global.json',
  'scripts/changed-file-route.mjs',
  'scripts/validate-dependabot-dotnet-repair-shared.mjs',
  'scripts/windows-language-model-surface-routing.mjs',
  'tests/runner-policy.test.mjs',
  'tests/windows-language-model-surface-routing.test.mjs',
]

export function shouldRunWindowsLanguageModelSurfaceProbe(paths) {
  return matchesAnyChangedPath(paths, windowsLanguageModelSurfacePaths)
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) {
  runChangedFileRoute({
    repositoryRoot: resolve(import.meta.dirname, '..'),
    select: shouldRunWindowsLanguageModelSurfaceProbe,
    logLabel: 'Windows language-model surface changed-file route',
    outputKey: 'windows-language-model-surface',
  })
}
