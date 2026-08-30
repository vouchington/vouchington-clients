import assert from 'node:assert/strict'
import { access, readFile } from 'node:fs/promises'
import { describe, it } from 'node:test'

const target = 'node_modules/vouchington-tooling/skills/github-actions-checklist/SKILL.md'

describe('shared workflow skill adapter', () => {
  it('points to the installed packaged checklist', async () => {
    const adapter = await readFile('.agents/skills/github-actions-checklist/SKILL.md', 'utf8')

    assert.match(adapter, new RegExp(target.replaceAll('.', '\\.'), 'u'))
    await access(target)
  })
})
