import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { resolve } from 'node:path'
import test from 'node:test'
import { promisify } from 'node:util'

const execFileAsync = promisify(execFile)
const repositoryRoot = resolve(import.meta.dirname, '..')

// An empty --checks value used to leave an empty array that macOS bash 3.2 treats as unbound
// under `set -u`, so the harness died with "unbound variable" instead of a usage error.
for (const harness of ['swift-clients/tooling/harness.sh', 'dotnet-clients/tooling/harness.sh']) {
  test(`${harness} rejects an empty --checks value`, async () => {
    await assert.rejects(
      () =>
        execFileAsync('/bin/bash', [resolve(repositoryRoot, harness), '--checks', ''], {
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
