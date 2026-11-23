import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import test from 'node:test'

const repositoryRoot = resolve(import.meta.dirname, '..')
const sevenDays = 7
const sevenDaysInMinutes = sevenDays * 24 * 60

test('pnpm waits seven days before adopting a release', async () => {
  const workspace = await readFile(join(repositoryRoot, 'pnpm-workspace.yaml'), 'utf8')
  const minutes = Number(/^minimumReleaseAge: (\d+)$/mu.exec(workspace)?.[1])
  assert.equal(minutes, sevenDaysInMinutes)
})

test('every Dependabot ecosystem entry sets a seven day cooldown', async () => {
  const config = await readFile(join(repositoryRoot, '.github/dependabot.yml'), 'utf8')
  const entries = config.match(/^ {2}- package-ecosystem: /gmu) ?? []
  const cooldowns = [...config.matchAll(/^ {6}default-days: (\d+)$/gmu)].map(m => Number(m[1]))
  assert.ok(entries.length > 0)
  assert.equal(cooldowns.length, entries.length)
  for (const days of cooldowns) assert.ok(days >= sevenDays)
  // Explicit SemVer cooldowns override default-days, so they must meet the same floor.
  for (const [line, days] of config.matchAll(/^\s+semver-(?:major|minor|patch)-days: (\d+)$/gmu)) {
    assert.ok(Number(days) >= sevenDays, `Dependabot override below seven days: ${line.trim()}`)
  }
})
