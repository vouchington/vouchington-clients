import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'
import { renamedFilePaths } from './git-rename-fixture.mjs'
import { shouldRunWindowsMauiBuild } from '../scripts/windows-maui-test-routing.mjs'

describe('Windows MAUI compile routing', () => {
  it('selects production, test, lock, and runner policy changes', () => {
    for (const path of [
      '.editorconfig',
      'dotnet-clients/.editorconfig',
      '.github/actions/prepare-native-contract/action.yml',
      'contracts/filaments.json',
      'docs/development/ci-runners.md',
      'docs/development/native-ci-test-placement.md',
      'dotnet-clients/AGENTS.md',
      'dotnet-clients/src/Voucha.Client.App/Support/LocalImagePreview.cs',
      'dotnet-clients/src/Voucha.Client.App/Support/WindowsLocalImagePreviewDecoder.cs',
      'dotnet-clients/src/Voucha.Client.Core/Chat/ChatConversationViewModel.cs',
      'dotnet-clients/src/Voucha.Client.App/Platforms/Windows/App.xaml.cs',
      'dotnet-clients/Directory.Build.targets',
      'global.json',
      '.mise.toml',
      'dotnet-clients/NuGet.config',
      '.github/workflows/native-contract-tests.yml',
    ])
      assert.equal(shouldRunWindowsMauiBuild([path]), true, path)
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
    assert.equal(shouldRunWindowsMauiBuild(paths), true)
  })

  it('skips unrelated Swift-only changes', () => {
    assert.equal(
      shouldRunWindowsMauiBuild([
        'swift-clients/apps/iOS/SettingsView.swift',
        'swift-clients/ui/Package.resolved',
        'dotnet-clients/tests/Voucha.Client.Core.Tests/ChatTests.cs',
      ]),
      false,
    )
  })
})

// Exercise the workflow's own jq fan-in, including selected jobs that are unexpectedly skipped.
it('fails the aggregate when a selected Windows build fails or is skipped', () => {
  const workflow = readFileSync(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  const expression = workflow
    .slice(workflow.lastIndexOf('jq -e --arg route'))
    .split("'\n")[1]
    .split("\n          '")[0]
  for (const [route, result, succeeds] of [
    ['false', 'skipped', true],
    ['true', 'success', true],
    ['true', 'skipped', false],
    ['true', 'failure', false],
    ['false', 'failure', false],
  ]) {
    const input = JSON.stringify({
      verify: { result: 'success' },
      'dotnet-windows-image-preview': { result: 'skipped' },
      'dotnet-windows-language-model-surface': { result: 'skipped' },
      'dotnet-windows-maui': { result },
    })
    let accepted = false
    try {
      execFileSync(
        'jq',
        [
          '-e',
          '--arg',
          'route',
          'false',
          '--arg',
          'maui',
          route,
          '--arg',
          'surface',
          'false',
          expression,
        ],
        {
          input,
          stdio: ['pipe', 'pipe', 'pipe'],
        },
      )
      accepted = true
    } catch (error) {
      assert.equal(error.status, 1)
    }
    assert.equal(accepted, succeeds, `${route}:${result}`)
  }
})
