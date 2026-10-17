import assert from 'node:assert/strict'
import { mkdtemp, readFile, rm, stat } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { writeExecutable, writeTree } from './fixture-files.mjs'

test('writeTree creates nested files with the given contents', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-fixture-files-'))
  t.after(() => rm(root, { recursive: true, force: true }))

  await writeTree(root, {
    'nested/dir/file.txt': 'hello\n',
    'top.txt': 'top\n',
  })

  assert.equal(await readFile(join(root, 'nested/dir/file.txt'), 'utf8'), 'hello\n')
  assert.equal(await readFile(join(root, 'top.txt'), 'utf8'), 'top\n')
})

test('writeExecutable marks the file executable', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-fixture-exec-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const path = join(root, 'tool')

  await writeExecutable(path, '#!/bin/sh\nexit 0\n')

  // Node on Windows does not report POSIX executable bits from stat().
  if (process.platform !== 'win32') {
    assert.equal((await stat(path)).mode & 0o111, 0o111)
  }
  assert.equal(await readFile(path, 'utf8'), '#!/bin/sh\nexit 0\n')
})
