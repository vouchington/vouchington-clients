import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
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
    const repository = mkdtempSync(join(tmpdir(), 'windows-maui-route-'))
    try {
      execFileSync('git', ['init', '-q'], { cwd: repository })
      execFileSync('git', ['config', 'user.email', 'test@example.invalid'], { cwd: repository })
      execFileSync('git', ['config', 'user.name', 'Routing test'], { cwd: repository })
      const source = join(repository, 'dotnet-clients/src/Voucha.Client.App/Support')
      mkdirSync(source, { recursive: true })
      writeFileSync(join(source, 'LocalImagePreview.cs'), 'before')
      execFileSync('git', ['add', '.'], { cwd: repository })
      execFileSync('git', ['commit', '-qm', 'baseline'], { cwd: repository })

      execFileSync(
        'git',
        [
          'mv',
          'dotnet-clients/src/Voucha.Client.App/Support/LocalImagePreview.cs',
          'retired-preview.cs',
        ],
        {
          cwd: repository,
        },
      )
      execFileSync('git', ['commit', '-qm', 'move decoder out'], { cwd: repository })
      const [base, revision] = execFileSync('git', ['rev-list', '--max-count=2', 'HEAD'], {
        cwd: repository,
        encoding: 'utf8',
      })
        .trim()
        .split('\n')
        .reverse()
      const paths = execFileSync(
        'git',
        ['diff', '--no-renames', '--name-only', '-z', base, revision],
        {
          cwd: repository,
          encoding: 'utf8',
        },
      )
        .split('\0')
        .filter(Boolean)

      assert.deepEqual(paths, [
        'dotnet-clients/src/Voucha.Client.App/Support/LocalImagePreview.cs',
        'retired-preview.cs',
      ])
      assert.equal(shouldRunWindowsMauiBuild(paths), true)
    } finally {
      rmSync(repository, { recursive: true, force: true })
    }
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
      'dotnet-windows-maui': { result },
    })
    let accepted = false
    try {
      execFileSync('jq', ['-e', '--arg', 'route', 'false', '--arg', 'maui', route, expression], {
        input,
        stdio: ['pipe', 'pipe', 'pipe'],
      })
      accepted = true
    } catch (error) {
      assert.equal(error.status, 1)
    }
    assert.equal(accepted, succeeds, `${route}:${result}`)
  }
})
