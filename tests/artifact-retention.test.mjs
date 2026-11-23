import assert from 'node:assert/strict'
import { readdir, readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import test from 'node:test'

const repositoryRoot = resolve(import.meta.dirname, '..')
// This repository is public, so artifacts are kept for the owner's 7-day public-repository window.
const retentionDays = 7

function uploadSteps(source) {
  const lines = source.split('\n')
  return lines.flatMap((line, index) => {
    if (!/^\s*(?:- )?uses: actions\/upload-artifact@/u.test(line)) return []
    // The step starts at the nearest `- ` at or above this line and ends before the next line that
    // is indented no deeper than that dash.
    let start = index
    while (start > 0 && !/^\s*- /u.test(lines[start])) start -= 1
    const indent = lines[start].search(/\S/u)
    const body = [lines[start]]
    for (const next of lines.slice(start + 1)) {
      if (next.trim() !== '' && next.search(/\S/u) <= indent) break
      body.push(next)
    }
    return [{ line: index + 1, step: body.join('\n') }]
  })
}

test('every artifact upload keeps artifacts for seven days', async () => {
  // Reads the files on disk rather than asking git, so an inherited GIT_DIR cannot select another checkout.
  const paths = (await readdir(join(repositoryRoot, '.github'), { recursive: true }))
    .filter(path => /\.ya?ml$/u.test(path))
    .map(path => join('.github', path))
    .sort()
  let uploads = 0
  for (const path of paths) {
    for (const { line, step } of uploadSteps(await readFile(join(repositoryRoot, path), 'utf8'))) {
      uploads += 1
      assert.match(
        step,
        new RegExp(`^\\s+retention-days: ${retentionDays}$`, 'mu'),
        `${path}:${line} must set retention-days: ${retentionDays}`,
      )
    }
  }
  assert.ok(uploads > 0, 'found no actions/upload-artifact steps')
})
