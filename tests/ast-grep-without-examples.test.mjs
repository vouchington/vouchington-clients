import assert from 'node:assert/strict'
import { mkdtemp, readFile, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import { stripExamples, writeScanConfig } from '../scripts/ast-grep-without-examples.mjs'

const repoRoot = fileURLToPath(new URL('..', import.meta.url))

describe('ast-grep scan without examples', () => {
  it('drops the examples block and keeps the rule body', () => {
    assert.equal(
      stripExamples('id: a\nrule:\n  kind: b\nexamples:\n  - code: x\n'),
      'id: a\nrule:\n  kind: b\n',
    )
    assert.equal(stripExamples('id: a\n'), 'id: a\n')
  })

  it('points the scan config at an examples-free copy of the rules', async t => {
    const directory = await mkdtemp(join(tmpdir(), 'voucha-ast-grep-config-test-'))
    t.after(() => rm(directory, { recursive: true, force: true }))
    const config = await readFile(await writeScanConfig(repoRoot, directory), 'utf8')
    assert.match(config, new RegExp(`ruleDirs:\\n  - "${join(directory, 'rules')}"\\n`))
    assert.match(config, /languageGlobs:/)
    const rule = await readFile(join(directory, 'rules', 'cs-core-no-platform-types.yml'), 'utf8')
    assert.doesNotMatch(rule, /^examples:/m)
  })
})
