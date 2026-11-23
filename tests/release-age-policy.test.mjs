import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import test from 'node:test'

const repositoryRoot = resolve(import.meta.dirname, '..')
const sevenDays = 7
const sevenDaysInMinutes = sevenDays * 24 * 60
// Reads each cooldown key's scalar value, ignoring quotes and a trailing YAML comment.
const cooldownKey = /^\s+(default-days|semver-(?:major|minor|patch)-days)\s*:\s*['"]?([^'"#\s]*)/gmu

test('pnpm waits seven days before adopting a release', async () => {
  const workspace = await readFile(join(repositoryRoot, 'pnpm-workspace.yaml'), 'utf8')
  const minutes = Number(/^minimumReleaseAge: (\d+)$/mu.exec(workspace)?.[1])
  assert.equal(minutes, sevenDaysInMinutes)
})

test('every Dependabot ecosystem entry sets a seven day cooldown', async () => {
  const config = await readFile(join(repositoryRoot, '.github/dependabot.yml'), 'utf8')
  const entries = config.split(/^ {2}- package-ecosystem:/mu).slice(1)
  assert.ok(entries.length > 0)
  for (const entry of entries) {
    const ecosystem = entry.trim().split(/\s/u)[0]
    const days = new Map([...entry.matchAll(cooldownKey)].map(([, key, value]) => [key, value]))
    assert.ok(days.has('default-days'), `${ecosystem} sets no default cooldown`)
    // Explicit SemVer cooldowns override default-days, so every value must meet the floor.
    for (const [key, value] of days) {
      assert.ok(Number(value) >= sevenDays, `${ecosystem} ${key} is ${value}, below seven days`)
    }
  }
})
