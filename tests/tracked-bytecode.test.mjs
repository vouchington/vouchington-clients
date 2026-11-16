import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'

const repositoryRoot = fileURLToPath(new URL('..', import.meta.url))

describe('tracked files', () => {
  it('never tracks Python bytecode', () => {
    const tracked = execFileSync('git', ['ls-files', '-z'], {
      cwd: repositoryRoot,
      encoding: 'utf8',
    })
      .split('\0')
      .filter(path => /(^|\/)__pycache__\/|\.py[cod]$/u.test(path))
    assert.deepEqual(tracked, [])
  })
})
