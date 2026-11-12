import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import {
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  symlinkSync,
  writeFileSync,
} from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { test } from 'node:test'
import { fileURLToPath } from 'node:url'
import { collectWindowsSymbols } from '../scripts/sentry-windows-symbols.mjs'

const app = 'dotnet-clients/src/Voucha.Client.App/bin/Release/net10.0-windows10.0.26100.0/win-x64'
const core = 'dotnet-clients/src/Voucha.Client.Core/bin/Release/net10.0'
const appName = 'Voucha.Client.App'
const coreName = 'Voucha.Client.Core'

function fixture(run) {
  const root = mkdtempSync(join(tmpdir(), 'windows-symbols-'))
  const output = join(root, 'collected')
  const pair = (directory, name, content = name) => {
    mkdirSync(join(root, directory), { recursive: true })
    for (const extension of ['pdb', 'dll']) {
      writeFileSync(join(root, directory, `${name}.${extension}`), `${content}.${extension}`)
    }
  }
  try {
    run({ root, output, pair })
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

test('collects same-build Windows DLL/PDB pairs and accepts byte-identical copies', () => {
  fixture(({ root, output, pair }) => {
    pair(app, appName)
    pair(app, coreName)
    pair(core, coreName)
    pair(join(app, 'nested'), coreName)
    collectWindowsSymbols(root, output)
    assert.deepEqual(readdirSync(output).sort(), [
      `${appName}.dll`,
      `${appName}.pdb`,
      `${coreName}.dll`,
      `${coreName}.pdb`,
    ])
    assert.equal(readFileSync(join(output, `${coreName}.dll`), 'utf8'), `${coreName}.dll`)
  })
})

test('rejects missing assemblies and incomplete DLL/PDB pairs before creating an artifact', () => {
  fixture(({ root, output, pair }) => {
    pair(app, appName)
    assert.throws(() => collectWindowsSymbols(root, output), /Missing Windows Release outputs/)
    pair(core, coreName)
    rmSync(join(root, core, `${coreName}.dll`))
    assert.throws(() => collectWindowsSymbols(root, output), /Missing DLL\/PDB pair/)
    assert.throws(() => readdirSync(output), { code: 'ENOENT' })
  })
})

for (const extension of ['pdb', 'dll']) {
  test(`rejects conflicting duplicate ${extension} files`, () => {
    fixture(({ root, output, pair }) => {
      pair(app, appName)
      pair(app, coreName)
      pair(core, coreName)
      writeFileSync(join(root, core, `${coreName}.${extension}`), 'different build')
      assert.throws(() => collectWindowsSymbols(root, output), /Conflicting build outputs/)
      assert.throws(() => readdirSync(output), { code: 'ENOENT' })
    })
  })
}

test('ignores Debug, obj, Catalyst, and other Windows RID outputs', () => {
  fixture(({ root, output, pair }) => {
    for (const directory of [
      app.replace('/Release/', '/Debug/'),
      app.replace('/bin/', '/obj/'),
      app.replace('net10.0-windows10.0.26100.0/win-x64', 'net10.0-maccatalyst/maccatalyst-arm64'),
      app.replace('win-x64', 'win-arm64'),
    ]) {
      pair(directory, appName)
      pair(directory, coreName)
    }
    assert.throws(() => collectWindowsSymbols(root, output), /Missing Windows Release outputs/)
    pair(app, appName)
    pair(core, coreName)
    const result = spawnSync(
      process.execPath,
      [
        fileURLToPath(new URL('../scripts/sentry-windows-symbols.mjs', import.meta.url)),
        root,
        output,
      ],
      { encoding: 'utf8' },
    )
    assert.equal(result.status, 0, result.stderr)
    assert.equal(readdirSync(output).length, 4)
  })
})

test('rejects symbolic links in build output', t => {
  fixture(({ root, output, pair }) => {
    pair(app, appName)
    pair(core, coreName)
    try {
      symlinkSync(join(root, core), join(root, app, 'linked-core'), 'junction')
    } catch (error) {
      if (process.platform === 'win32' && ['EPERM', 'EACCES', 'ENOTSUP'].includes(error.code)) {
        t.skip(`Windows cannot create the test symbolic link: ${error.code}`)
        return
      }
      throw error
    }
    assert.throws(() => collectWindowsSymbols(root, output), /Symbolic link in build output/)
  })
})

test('rejects an existing destination without replacing its contents', () => {
  fixture(({ root, output, pair }) => {
    pair(app, appName)
    pair(core, coreName)
    mkdirSync(output)
    writeFileSync(join(output, 'existing'), 'preserve')
    assert.throws(() => collectWindowsSymbols(root, output), { code: 'EEXIST' })
    assert.deepEqual(readdirSync(output), ['existing'])
    assert.equal(readFileSync(join(output, 'existing'), 'utf8'), 'preserve')
  })
})

test('CLI rejects wrong argument counts', () => {
  const script = fileURLToPath(new URL('../scripts/sentry-windows-symbols.mjs', import.meta.url))
  for (const args of [[], ['root'], ['root', 'destination', 'extra']]) {
    const result = spawnSync(process.execPath, [script, ...args], { encoding: 'utf8' })
    assert.notEqual(result.status, 0)
    assert.match(result.stderr, /Usage: sentry-windows-symbols\.mjs ROOT DESTINATION/)
  }
})
