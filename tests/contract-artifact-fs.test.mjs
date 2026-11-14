import assert from 'node:assert/strict'
import { mkdir, mkdtemp, rm, symlink } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { artifactEntries, treeFiles } from '../scripts/contract-artifact-fs.mjs'
import { writeTree } from './fixture-files.mjs'

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), 'voucha-artifact-tree-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  return root
}

test('source and artifact walks sort nested paths and retain empty artifact directories', async t => {
  const root = await fixture(t)
  await writeTree(root, { 'z.json': '{}', 'nested/b.json': '{}', 'nested/a.json': '{}' })
  await mkdir(join(root, 'empty'))
  const files = ['nested/a.json', 'nested/b.json', 'z.json']
  assert.deepEqual(await treeFiles(root, 'source'), files)
  assert.deepEqual(await artifactEntries(root), { directories: ['empty', 'nested'], files })
})

test('source and artifact walks reject nested symlinks with their original diagnostics', async t => {
  const root = await fixture(t)
  await writeTree(root, { 'nested/file.json': '{}' })
  await symlink('file.json', join(root, 'nested/alias.json'))
  await assert.rejects(treeFiles(root, 'source'), {
    message: 'Contract artifact failed: symbolic link found at source/nested/alias.json',
  })
  await assert.rejects(artifactEntries(root), {
    message: 'Contract artifact failed: symbolic link found at artifact path nested/alias.json',
  })
})

test('source and artifact walks reject a symlinked root before traversal', async t => {
  const root = await fixture(t)
  await mkdir(join(root, 'target'))
  const alias = join(root, 'alias')
  await symlink('target', alias)
  await assert.rejects(treeFiles(alias, 'source'), {
    message: 'Contract artifact failed: symbolic link found at source',
  })
  await assert.rejects(artifactEntries(alias), {
    message: 'Contract artifact failed: symbolic link found at artifact root',
  })
})
