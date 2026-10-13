import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
import { shouldRunWindowsImagePreviewTests } from '../scripts/windows-image-preview-test-routing.mjs'

describe('Windows image preview test routing', () => {
  it('selects production, test, lock, and runner policy changes', () => {
    for (const path of [
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
    const repository = mkdtempSync(join(tmpdir(), 'windows-image-preview-route-'))
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
      assert.equal(shouldRunWindowsImagePreviewTests(paths), true)
    } finally {
      rmSync(repository, { recursive: true, force: true })
    }
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
