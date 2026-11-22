import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import { isDocsOnly, mergeGroupChangedPaths } from '../scripts/docs-only-routing.mjs'
import { jobBlock } from './workflow-job-block.mjs'

const native = await readFile(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)
const script = fileURLToPath(new URL('../scripts/docs-only-routing.mjs', import.meta.url))
const jobs = [...native.slice(native.search(/^jobs:/mu)).matchAll(/^ {2}([a-z0-9-]+):$/gmu)].map(
  match => match[1],
)
const header = name => jobBlock(native, name).split('\n    steps:')[0]
const needsOf = name => {
  const text = header(name)
  const inline = text.match(/^ {4}needs: (.+)$/mu)?.[1]
  if (inline)
    return inline
      .replace(/[[\]]/gu, '')
      .split(',')
      .map(item => item.trim())
  const list = text.match(/^ {4}needs:\n((?: {6}- .+\n?)+)/mu)?.[1] ?? ''
  return [...list.matchAll(/- ([\w-]+)/gu)].map(match => match[1])
}
const gate = "needs.verify.outputs.docs-only != 'true'"
// produce and verify must keep running; push-only jobs never see a docs-only route.
const exempt = new Set(['produce', 'verify'])

describe('docs-only routing', () => {
  it('is docs-only only when every path is Markdown or under docs/', () => {
    for (const paths of [
      ['README.md'],
      ['swift-clients/AGENTS.md', 'docs/development/ci-runners.md'],
      ['docs/diagram.png'],
      ['a/b/c.md', 'CONTRIBUTING.md'],
    ])
      assert.equal(isDocsOnly(paths), true, paths.join())
  })

  it('runs the full suite for a mixed or non-Markdown change', () => {
    for (const paths of [
      [],
      ['README.md', 'swift-clients/ui/Package.swift'],
      ['docs/a.md', '.github/workflows/native-contract-tests.yml'],
      ['contracts/vouchington.json'],
      ['dotnet-clients/src/Voucha.Client.App/README.txt'],
      ['README.md.bak'],
      ['docs.md.swift'],
      ['mydocs/file.cs'],
      ['README.MD'],
    ])
      assert.equal(isDocsOnly(paths), false, paths.join())
  })

  it('fails safe to not docs-only when the diff cannot be computed', () => {
    const output = join(mkdtempSync(join(tmpdir(), 'docs-only-')), 'output')
    const result = spawnSync(process.execPath, [script, 'main', 'HEAD'], {
      env: { ...process.env, GITHUB_OUTPUT: output },
      encoding: 'utf8',
    })
    assert.equal(result.status, 0)
    assert.equal(readFileSync(output, 'utf8'), 'docs-only=false\n')
  })

  it('routes a merge group on the whole queued ref over main, not one entry', () => {
    const root = mkdtempSync(join(tmpdir(), 'docs-only-queue-'))
    const env = {
      ...process.env,
      GIT_AUTHOR_NAME: 't',
      GIT_AUTHOR_EMAIL: 't@example.com',
      GIT_COMMITTER_NAME: 't',
      GIT_COMMITTER_EMAIL: 't@example.com',
    }
    const run = (cwd, ...args) =>
      execFileSync('git', args, {
        cwd,
        env,
        encoding: 'utf8',
        stdio: ['ignore', 'pipe', 'pipe'],
      }).trim()
    const origin = join(root, 'origin.git')
    const work = join(root, 'work')
    run(root, 'init', '-q', '--bare', '-b', 'main', origin)
    run(root, 'clone', '-q', origin, work)
    const commit = (file, message) => {
      writeFileSync(join(work, file), message)
      run(work, 'add', file)
      run(work, 'commit', '-q', '-m', message)
      return run(work, 'rev-parse', 'HEAD')
    }
    commit('README.md', 'base')
    run(work, 'push', '-q', 'origin', 'HEAD:main')
    run(work, 'checkout', '-q', '-b', 'queue')
    const codeEntry = commit('App.swift', 'code entry')
    // The docs entry's merge_group.base_sha is the code entry's group commit.
    const docsEntry = commit('NOTES.md', 'docs entry')
    run(work, 'push', '-q', 'origin', 'queue')
    const consumer = join(root, 'consumer')
    run(root, 'clone', '-q', '--depth=1', `file://${origin}`, consumer)
    const route = revision =>
      isDocsOnly(mergeGroupChangedPaths({ repositoryRoot: consumer, revision }))
    assert.deepEqual(
      mergeGroupChangedPaths({ repositoryRoot: consumer, revision: docsEntry }).sort(),
      ['App.swift', 'NOTES.md'],
    )
    assert.equal(route(docsEntry), false, 'docs entry on top of a code entry must run native jobs')
    assert.equal(isDocsOnly(['NOTES.md']), true, 'entry-only diff would have skipped them')
    assert.equal(route(codeEntry), false)
    assert.throws(() =>
      mergeGroupChangedPaths({ repositoryRoot: consumer, revision: 'b'.repeat(40) }),
    )
    run(work, 'checkout', '-q', '-b', 'docs-queue', 'main')
    const docsOnly = commit('docs-note.md', 'docs only')
    run(work, 'push', '-q', 'origin', 'docs-queue')
    assert.equal(route(docsOnly), true)
  })

  it('computes the route from the verified candidate range and never on a push', () => {
    const verify = jobBlock(native, 'verify')
    assert.ok(
      verify.includes(
        `docs-only: \${{ contains(fromJSON('["pull_request","merge_group"]'), github.event_name) && steps.docs-only-route.outputs.docs-only == 'true' }}`,
      ),
    )
    const step = verify.slice(verify.indexOf('- name: Route docs-only changes'))
    const body = step.split(/\n {6}- name:/u)[0]
    assert.ok(body.includes('BASE_SHA: ${{ steps.identity.outputs.base_sha }}'))
    assert.ok(body.includes('REVISION_SHA: ${{ steps.identity.outputs.revision_sha }}'))
    assert.ok(body.includes('EVENT_NAME: ${{ github.event_name }}'))
    assert.ok(body.includes('node scripts/docs-only-routing.mjs "$BASE_SHA" "$REVISION_SHA"'))
    assert.ok(verify.includes('${{ github.event.merge_group.base_sha }}'))
  })

  it('skips every native job on a docs-only change while produce and verify keep running', () => {
    const gated = new Set()
    for (const name of jobs) {
      if (exempt.has(name)) continue
      if (header(name).includes(gate)) gated.add(name)
    }
    for (const name of jobs) {
      if (exempt.has(name) || name === 'tests') continue
      const reaches = gated.has(name) || needsOf(name).some(need => gated.has(need))
      assert.ok(reaches, `${name} must be skipped on a docs-only change`)
    }
    for (const name of ['produce', 'verify', 'tests']) {
      assert.ok(!header(name).includes(gate), name)
    }
    // A gate is a pure addition: the push-only and cancellation conditions stay intact.
    for (const name of gated) {
      const condition = header(name).match(/^ {4}if: (.+)$/mu)[1]
      assert.ok(condition.includes(gate), name)
    }
  })

  it('keeps jobs that run through a skipped dependency from running on a docs-only change', () => {
    for (const name of jobs) {
      const condition = header(name).match(/^ {4}if: (.+)$/mu)?.[1] ?? ''
      if (!condition.includes('!cancelled()') || name === 'tests') continue
      assert.ok(condition.includes(gate), `${name} would run despite skipped needs`)
      assert.ok(needsOf(name).includes('verify'), `${name} must read the verify route`)
    }
  })
})
