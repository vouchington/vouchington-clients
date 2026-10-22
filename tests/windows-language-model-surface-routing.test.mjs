import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
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
    const repository = mkdtempSync(join(tmpdir(), 'windows-language-model-surface-route-'))
    try {
      execFileSync('git', ['init', '-q'], { cwd: repository })
      execFileSync('git', ['config', 'user.email', 'test@example.invalid'], { cwd: repository })
      execFileSync('git', ['config', 'user.name', 'Routing test'], { cwd: repository })
      const source = join(repository, 'dotnet-clients/src/Voucha.Client.App/Chat')
      mkdirSync(source, { recursive: true })
      writeFileSync(join(source, 'WindowsSystemLanguageModelRuntime.Windows.cs'), 'before')
      execFileSync('git', ['add', '.'], { cwd: repository })
      execFileSync('git', ['commit', '-qm', 'baseline'], { cwd: repository })

      execFileSync(
        'git',
        [
          'mv',
          'dotnet-clients/src/Voucha.Client.App/Chat/WindowsSystemLanguageModelRuntime.Windows.cs',
          'retired-runtime.cs',
        ],
        {
          cwd: repository,
        },
      )
      execFileSync('git', ['commit', '-qm', 'move runtime out'], { cwd: repository })
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
        'dotnet-clients/src/Voucha.Client.App/Chat/WindowsSystemLanguageModelRuntime.Windows.cs',
        'retired-runtime.cs',
      ])
      assert.equal(shouldRunWindowsLanguageModelSurfaceProbe(paths), true)
    } finally {
      rmSync(repository, { recursive: true, force: true })
    }
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
