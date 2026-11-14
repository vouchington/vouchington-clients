import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { renamedFilePaths } from './git-rename-fixture.mjs'
import { shouldRunWindowsImagePreviewTests } from '../scripts/windows-image-preview-test-routing.mjs'

describe('Windows image preview test routing', () => {
  it('selects production, test, lock, and runner policy changes', () => {
    for (const path of [
      '.editorconfig',
      'dotnet-clients/.editorconfig',
      '.github/actions/prepare-native-contract/action.yml',
      'dotnet-clients/src/Voucha.Client.App/Support/LocalImagePreview.cs',
      'dotnet-clients/src/Voucha.Client.App/Support/WindowsLocalImagePreviewDecoder.cs',
      'dotnet-clients/tests/Voucha.Client.WindowsImagePreview.Tests/WindowsLocalImagePreviewDecoderTests.cs',
      'dotnet-clients/tests/Voucha.Client.WindowsImagePreview.Tests/packages.lock.json',
      'global.json',
      '.mise.toml',
      'dotnet-clients/NuGet.config',
      'dotnet-clients/Directory.Build.targets',
      '.github/workflows/native-contract-tests.yml',
    ])
      assert.equal(shouldRunWindowsImagePreviewTests([path]), true, path)
  })

  it('selects a relevant file renamed out of the source tree', () => {
    const paths = renamedFilePaths(
      'dotnet-clients/src/Voucha.Client.App/Support/LocalImagePreview.cs',
      'retired-preview.cs',
    )
    assert.deepEqual(paths, [
      'dotnet-clients/src/Voucha.Client.App/Support/LocalImagePreview.cs',
      'retired-preview.cs',
    ])
    assert.equal(shouldRunWindowsImagePreviewTests(paths), true)
  })

  it('skips unrelated Swift-only changes', () => {
    assert.equal(
      shouldRunWindowsImagePreviewTests([
        'swift-clients/apps/iOS/SettingsView.swift',
        'swift-clients/ui/Package.resolved',
      ]),
      false,
    )
  })
})
