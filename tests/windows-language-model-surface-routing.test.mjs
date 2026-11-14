import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { renamedFilePaths } from './git-rename-fixture.mjs'
import { shouldRunWindowsLanguageModelSurfaceProbe } from '../scripts/windows-language-model-surface-routing.mjs'

describe('Windows language-model surface probe routing', () => {
  it('selects the pinned surface, runtime, lock, and runner policy changes', () => {
    for (const path of [
      '.editorconfig',
      'dotnet-clients/.editorconfig',
      '.github/actions/prepare-native-contract/action.yml',
      'dotnet-clients/src/Voucha.Client.App/Chat/WindowsSystemLanguageModelRuntime.Windows.cs',
      'dotnet-clients/src/Voucha.Client.Core/Chat/WindowsSystemLanguageModelProvider.cs',
      'dotnet-clients/tests/Voucha.Client.WindowsLanguageModelSurface.Tests/WindowsLanguageModelSurfaceProbeTests.cs',
      'dotnet-clients/tests/Voucha.Client.WindowsLanguageModelSurface.Tests/packages.lock.json',
      'dotnet-clients/Directory.Packages.props',
      'global.json',
      '.mise.toml',
      'dotnet-clients/NuGet.config',
      'dotnet-clients/Directory.Build.targets',
      '.github/workflows/native-contract-tests.yml',
      'scripts/validate-dependabot-dotnet-repair-shared.mjs',
    ])
      assert.equal(shouldRunWindowsLanguageModelSurfaceProbe([path]), true, path)
  })

  it('selects a relevant file renamed out of the source tree', () => {
    const paths = renamedFilePaths(
      'dotnet-clients/src/Voucha.Client.App/Chat/WindowsSystemLanguageModelRuntime.Windows.cs',
      'retired-runtime.cs',
    )
    assert.deepEqual(paths, [
      'dotnet-clients/src/Voucha.Client.App/Chat/WindowsSystemLanguageModelRuntime.Windows.cs',
      'retired-runtime.cs',
    ])
    assert.equal(shouldRunWindowsLanguageModelSurfaceProbe(paths), true)
  })

  it('skips unrelated Swift-only changes', () => {
    assert.equal(
      shouldRunWindowsLanguageModelSurfaceProbe([
        'swift-clients/apps/iOS/SettingsView.swift',
        'swift-clients/ui/Package.resolved',
      ]),
      false,
    )
  })
})
