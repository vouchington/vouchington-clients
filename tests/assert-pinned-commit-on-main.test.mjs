import { mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import assert from 'node:assert/strict'
import test from 'node:test'

import { assertPinnedCommitOnMain } from '../scripts/assert-pinned-commit-on-main.mjs'
import {
  contractRepository,
  declaredContractPaths,
  sameDeclarationIgnoringPin,
} from '../scripts/contract-config.mjs'

const commit = 'b'.repeat(40)

async function config(t, overrides = {}) {
  const directory = await mkdtemp(join(tmpdir(), 'voucha-pin-'))
  t.after(() => rm(directory, { recursive: true, force: true }))
  const file = join(directory, 'vouchington.json')
  await writeFile(
    file,
    JSON.stringify({
      schemaVersion: 1,
      repository: contractRepository,
      commit,
      paths: declaredContractPaths,
      ...overrides,
    }),
  )
  return file
}

test('accepts a pin that is main or an ancestor of main', async t => {
  const file = await config(t)
  for (const status of ['identical', 'ahead']) {
    const calls = []
    const compare = (...args) => (calls.push(args), status)
    assert.equal(await assertPinnedCommitOnMain({ config: file, compare }), commit)
    assert.deepEqual(calls, [[contractRepository, commit]])
  }
})

test('rejects a pin that is not reachable from main, fail closed on API errors', async t => {
  const file = await config(t)
  for (const status of ['behind', 'diverged', '', 'unknown'])
    await assert.rejects(
      assertPinnedCommitOnMain({ config: file, compare: () => status }),
      /not reachable from vouchington\/vouchington@main/,
    )
  await assert.rejects(
    assertPinnedCommitOnMain({
      config: file,
      compare: () => {
        throw new Error('HTTP 404')
      },
    }),
    /cannot compare .* HTTP 404/,
  )
})

test('rejects a malformed pin before asking GitHub', async t => {
  for (const commitValue of ['main', 'B'.repeat(40), 'b'.repeat(7)])
    await assert.rejects(
      assertPinnedCommitOnMain({
        config: await config(t, { commit: commitValue }),
        compare: () => assert.fail('must not call GitHub'),
      }),
      /commit must be a full SHA/,
    )
})

test('declarations match ignoring only the pin', () => {
  const base = {
    schemaVersion: 1,
    repository: contractRepository,
    commit,
    paths: declaredContractPaths,
  }
  assert.ok(sameDeclarationIgnoringPin(base, { ...base, commit: 'c'.repeat(40) }))
  assert.ok(!sameDeclarationIgnoringPin(base, { ...base, paths: declaredContractPaths.slice(1) }))
  assert.ok(!sameDeclarationIgnoringPin(base, { ...base, repository: 'other/repo' }))
})
