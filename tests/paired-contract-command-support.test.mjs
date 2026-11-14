import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtemp, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
import { decodeSnapshot, git, readJson } from '../scripts/paired-contract-command-support.mjs'

const snapshotKey = 'PAIR_SNAPSHOT_BASE64'

describe('paired contract command support', { concurrency: 1 }, () => {
  it('reads trimmed git output', async () => {
    const root = await mkdtemp(join(tmpdir(), 'paired-contract-git-'))
    try {
      execFileSync('git', ['init', '-q'], { cwd: root })
      assert.equal(git(root, ['rev-parse', '--is-inside-work-tree']), 'true')
    } finally {
      await rm(root, { recursive: true, force: true })
    }
  })

  it('reads one regular JSON file and preserves read failures', async () => {
    const root = await mkdtemp(join(tmpdir(), 'paired-contract-json-'))
    try {
      const file = join(root, 'value.json')
      const link = join(root, 'value-link.json')
      const invalid = join(root, 'invalid.json')
      await writeFile(file, '{"ok":true}\n')
      await writeFile(invalid, 'not-json')
      await symlink(file, link)

      assert.deepEqual(await readJson(file, 'sample'), { ok: true })
      await assert.rejects(readJson(root, 'sample directory'), {
        message:
          'Paired contract attestation failed: cannot read sample directory: Paired contract attestation failed: sample directory must be a regular file',
      })
      await assert.rejects(readJson(link, 'sample link'), {
        message:
          'Paired contract attestation failed: cannot read sample link: Paired contract attestation failed: sample link must be a regular file',
      })
      await assert.rejects(
        readJson(join(root, 'missing.json'), 'missing sample'),
        /cannot read missing sample/u,
      )
      await assert.rejects(readJson(invalid, 'invalid sample'), /cannot read invalid sample/u)
    } finally {
      await rm(root, { recursive: true, force: true })
    }
  })

  it('decodes one exact base64url pair snapshot', async () => {
    const encoded = Buffer.from('{"pair":1}', 'utf8').toString('base64url')
    await withSnapshot(`  ${encoded}  `, () => {
      assert.deepEqual(decodeSnapshot(), { pair: 1 })
    })
  })

  it('rejects a missing, mis-encoded, or non-JSON pair snapshot', async () => {
    await withSnapshot(undefined, () => {
      assert.throws(
        () => decodeSnapshot(),
        /Paired contract attestation failed: captured pair snapshot is required/u,
      )
    })
    await withSnapshot('   ', () => {
      assert.throws(() => decodeSnapshot(), /captured pair snapshot is required/u)
    })
    await withSnapshot(`${Buffer.from('{"pair":1}', 'utf8').toString('base64url')}=`, () => {
      assert.throws(() => decodeSnapshot(), /captured pair snapshot encoding is invalid/u)
    })
    await withSnapshot(Buffer.from('not-json', 'utf8').toString('base64url'), () => {
      assert.throws(() => decodeSnapshot(), /captured pair snapshot is invalid/u)
    })
  })
})

async function withSnapshot(value, run) {
  const previous = process.env[snapshotKey]
  if (value === undefined) delete process.env[snapshotKey]
  else process.env[snapshotKey] = value
  try {
    await run()
  } finally {
    if (previous === undefined) delete process.env[snapshotKey]
    else process.env[snapshotKey] = previous
  }
}
