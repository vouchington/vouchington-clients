import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'

const script = new URL('../dotnet-clients/tooling/catalyst-compile-state.sh', import.meta.url)
const revision = 'a'.repeat(40)
const identity = [
  revision,
  '39700146375',
  '1',
  'maccatalyst-arm64',
  '10.0.401',
  '10.0.401',
  'Xcode 26.6',
]

const invoke = (mode, root, state, fields = identity) =>
  spawnSync('bash', [script.pathname, mode, root, state, ...fields], {
    encoding: 'utf8',
  })

it('rehydrates only matching exact-run managed compile outputs', async () => {
  const temp = await mkdtemp(join(tmpdir(), 'catalyst-compile-state-'))
  const source = join(temp, 'source')
  const destination = join(temp, 'destination')
  const state = join(temp, 'state')
  try {
    const app = join(source, 'obj', 'Voucha.Client.App')
    const core = join(source, 'obj', 'Voucha.Client.Core')
    const coreBin = join(source, 'bin', 'Voucha.Client.Core')
    await Promise.all([
      mkdir(join(app, 'release_net10.0-maccatalyst_maccatalyst-arm64'), { recursive: true }),
      mkdir(core, { recursive: true }),
      mkdir(coreBin, { recursive: true }),
    ])
    await Promise.all([
      writeFile(join(app, 'project.assets.json'), '{}'),
      writeFile(join(core, 'project.assets.json'), '{}'),
      writeFile(
        join(app, 'release_net10.0-maccatalyst_maccatalyst-arm64', 'Voucha.Client.App.dll'),
        'app',
      ),
      writeFile(join(coreBin, 'Voucha.Client.Core.dll'), 'core'),
    ])

    assert.equal(invoke('pack', source, state).status, 0)
    assert.notEqual(
      invoke('unpack', destination, state, [revision, '39700146375', '2', ...identity.slice(3)])
        .status,
      0,
    )
    assert.equal(invoke('unpack', destination, state).status, 0)
    assert.equal(
      await readFile(
        join(
          destination,
          'obj',
          'Voucha.Client.App',
          'release_net10.0-maccatalyst_maccatalyst-arm64',
          'Voucha.Client.App.dll',
        ),
        'utf8',
      ),
      'app',
    )

    await writeFile(join(state, 'compile-state.tar.gz'), 'corrupted')
    assert.notEqual(invoke('unpack', join(temp, 'corrupt-destination'), state).status, 0)
  } finally {
    await rm(temp, { recursive: true, force: true })
  }
})
