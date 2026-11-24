import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { resolve } from 'node:path'
import test from 'node:test'
import { promisify } from 'node:util'

const execFileAsync = promisify(execFile)
const repositoryRoot = resolve(import.meta.dirname, '..')

// An empty --checks value, or one that splits into no fields such as a lone newline, used to leave
// an empty array that macOS bash 3.2 treats as unbound under `set -u`, so the harness died with
// "unbound variable" instead of a usage error.
const values = [
  ['an empty', ''],
  ['a newline-only', '\n'],
]
for (const harness of ['swift-clients/tooling/harness.sh', 'dotnet-clients/tooling/harness.sh']) {
  for (const [label, value] of values) {
    test(`${harness} rejects ${label} --checks value`, async () => {
      await assert.rejects(
        () =>
          execFileAsync('/bin/bash', [resolve(repositoryRoot, harness), '--checks', value], {
            cwd: repositoryRoot,
          }),
        error => {
          assert.equal(error.code, 1)
          assert.match(error.stderr, /--checks requires an argument/u)
          assert.doesNotMatch(error.stderr, /unbound variable/u)
          return true
        },
      )
    })
  }
}
