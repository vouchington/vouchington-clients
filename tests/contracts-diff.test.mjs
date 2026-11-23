import { execFile } from 'node:child_process'
import { mkdtemp, readFile, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { promisify } from 'node:util'
import assert from 'node:assert/strict'
import test from 'node:test'

import { declaredContractPaths, localizationPaths } from '../scripts/contract-config.mjs'
import { diffContracts } from '../scripts/contracts-diff.mjs'
import { writeTree } from './fixture-files.mjs'
import { git } from './merge-queue-fixture.mjs'

const run = promisify(execFile)
const script = resolve(import.meta.dirname, '../scripts/contracts-diff.mjs')
const shas = { one: '1'.repeat(40), two: '2'.repeat(40) }
const [swiftDir, dotnetDir] = localizationPaths

const manifest = routes =>
  JSON.stringify({ fixtures: Object.entries(routes).map(([id, route]) => ({ id, route })) })

function producer(overrides = {}) {
  // A null override removes the file from the producer tree.
  const files = {
    'api-fixtures/v1/manifest.json': manifest({ 'native.auth.route-only': 'GET /a' }),
    'api-fixtures/v1/responses/native.auth.email-address.tokens.json': '{"a":1}\n',
    'api-fixtures/v1/responses/native.auth.sessions.default.json': '{"s":1}\n',
    'api-contracts/v1/request-contracts.json': '{"v":1}\n',
    [`${swiftDir}/Resources/en.lproj/Localizable.strings`]: '"a" = "A";\n',
    [`${dotnetDir}/UiMessages.resx`]: '<root/>\n',
    ...overrides,
  }
  return Object.fromEntries(Object.entries(files).filter(([, contents]) => contents !== null))
}

async function scratch(t, prefix) {
  const directory = await mkdtemp(join(tmpdir(), prefix))
  t.after(() => rm(directory, { recursive: true, force: true }))
  return directory
}

// A consumer repository whose history pins the given producer commits, one revision each.
async function consumer(t, pins) {
  const root = await scratch(t, 'voucha-diff-consumer-')
  git(root, 'init', '-q', '-b', 'main')
  for (const [name, commit] of Object.entries(pins)) {
    await writeTree(root, {
      'contracts/vouchington.json': JSON.stringify({
        schemaVersion: 1,
        repository: 'vouchington/vouchington',
        commit,
        paths: declaredContractPaths,
      }),
    })
    git(root, 'add', '-A')
    git(root, 'commit', '-q', '--allow-empty', '-m', name)
    git(root, 'tag', name)
  }
  return root
}

async function fakeProducers(t, trees) {
  const checkouts = {}
  for (const [sha, files] of Object.entries(trees)) {
    checkouts[sha] = await scratch(t, 'voucha-diff-producer-')
    await writeTree(checkouts[sha], files)
  }
  const fetched = []
  const fetchPinned = async pin => {
    fetched.push(pin.commit)
    if (!checkouts[pin.commit]) throw new Error(`fetch failed for ${pin.commit}`)
    return checkouts[pin.commit]
  }
  return { checkouts, fetched, fetchPinned }
}

test('reports changed, added and removed fixtures, localization files and contracts', async t => {
  const { fetched, fetchPinned, checkouts } = await fakeProducers(t, {
    [shas.one]: producer(),
    [shas.two]: producer({
      'api-fixtures/v1/responses/native.auth.email-address.tokens.json': '{"a":2}\n',
      'api-fixtures/v1/responses/native.auth.new.json': '{}\n',
      'api-fixtures/v1/responses/native.auth.sessions.default.json': null,
      'api-contracts/v1/request-contracts.json': '{"v":2}\n',
      'api-contracts/v1/extra/schema.json': '{}\n',
      'api-fixtures/v1/manifest.json': manifest({ 'native.auth.route-only': 'GET /b' }),
      [`${swiftDir}/Resources/en.lproj/Localizable.strings`]: '"a" = "B";\n',
      [`${dotnetDir}/UiMessages.fr.resx`]: '<root/>\n',
    }),
  })
  const gitRoot = await consumer(t, { base: shas.one, head: shas.two })
  const { report, changes } = await diffContracts({
    base: 'base',
    head: 'head',
    withChanges: true,
    gitRoot,
    cacheRoot: '/unused',
    fetchPinned,
  })
  assert.deepEqual(fetched, [shas.one, shas.two])
  assert.equal(report.complete, true)
  assert.deepEqual(report.base, { revision: 'base', commit: shas.one, root: checkouts[shas.one] })
  assert.deepEqual(changes, {
    schemaVersion: 1,
    base: { revision: 'base', commit: shas.one },
    head: { revision: 'head', commit: shas.two },
    fixtures: {
      changed: ['native.auth.email-address.tokens', 'native.auth.route-only'],
      added: ['native.auth.new'],
      removed: ['native.auth.sessions.default'],
    },
    localization: {
      changed: [`${swiftDir}/Resources/en.lproj/Localizable.strings`],
      added: [`${dotnetDir}/UiMessages.fr.resx`],
      removed: [],
    },
    contracts: {
      changed: ['api-fixtures/v1/manifest.json', 'request-contracts.json'],
      added: ['extra/schema.json'],
      removed: [],
    },
    complete: true,
  })
})

test('the same commit on both sides is complete with empty lists', async t => {
  const { fetchPinned } = await fakeProducers(t, { [shas.one]: producer() })
  const gitRoot = await consumer(t, { base: shas.one, head: shas.one })
  const { report, changes } = await diffContracts({
    base: 'base',
    head: 'head',
    withChanges: true,
    gitRoot,
    cacheRoot: '/unused',
    fetchPinned,
  })
  assert.equal(report.complete, true)
  for (const category of ['fixtures', 'localization', 'contracts'])
    assert.deepEqual(changes[category], { changed: [], added: [], removed: [] })
})

test('without --changes no producer tree is staged or compared', async t => {
  const { fetchPinned } = await fakeProducers(t, { [shas.one]: producer(), [shas.two]: {} })
  const gitRoot = await consumer(t, { base: shas.one, head: shas.two })
  const { report } = await diffContracts({ base: 'base', head: 'head', gitRoot, fetchPinned })
  assert.equal(report.complete, true)
})

test('a fetch failure reports an incomplete diff with empty lists', async t => {
  const { fetchPinned } = await fakeProducers(t, { [shas.one]: producer() })
  const gitRoot = await consumer(t, { base: shas.one, head: shas.two })
  const { report, changes } = await diffContracts({
    base: 'base',
    head: 'head',
    withChanges: true,
    gitRoot,
    fetchPinned,
  })
  assert.equal(report.complete, false)
  assert.match(report.error, /fetch failed/)
  assert.equal(changes.complete, false)
  assert.deepEqual(changes.fixtures, { changed: [], added: [], removed: [] })
})

test('a missing pin at the base revision exits non-zero with valid incomplete JSON', async t => {
  const out = join(await scratch(t, 'voucha-diff-out-'), 'changes.json')
  const result = await run(
    process.execPath,
    [script, '--base', 'no-such-revision', '--head', 'HEAD', '--changes', out],
    { cwd: resolve(import.meta.dirname, '..') },
  ).catch(error => error)
  assert.equal(result.code, 1)
  const report = JSON.parse(result.stdout)
  assert.equal(report.complete, false)
  assert.match(report.error, /no valid contract pin at no-such-revision/)
  const changes = JSON.parse(await readFile(out, 'utf8'))
  assert.equal(changes.complete, false)
  assert.equal(changes.base.commit, null)
})

test('a revision that looks like an option is rejected as an invalid pin', async t => {
  const gitRoot = await consumer(t, { base: shas.one })
  const { report } = await diffContracts({ base: '--output=x', head: 'base', gitRoot })
  assert.equal(report.complete, false)
  assert.match(report.error, /must not start with a dash/)
})

test('missing or unknown arguments print usage', async () => {
  for (const args of [[], ['--base', 'a'], ['--base', 'a', '--head', 'b', '--bogus', 'c']]) {
    const result = await run(process.execPath, [script, ...args]).catch(error => error)
    assert.notEqual(result.code, 0)
    assert.match(result.stderr, /Usage: node scripts\/contracts-diff\.mjs/)
  }
})

test('a client ref that moves during the producer fetch still stages the resolved commit', async t => {
  const { fetchPinned: fetchProducer } = await fakeProducers(t, {
    [shas.one]: producer(),
    [shas.two]: producer({ 'api-fixtures/v1/responses/native.auth.new.json': '{}\n' }),
  })
  const gitRoot = await consumer(t, { base: shas.one, head: shas.two })
  // Deleting the tag mid-run makes any later lookup by name fail; the resolved commit survives.
  const fetchPinned = async pin => {
    if (pin.commit === shas.two) git(gitRoot, 'tag', '-d', 'base')
    return fetchProducer(pin)
  }
  const { report, changes } = await diffContracts({
    base: 'base',
    head: 'head',
    withChanges: true,
    gitRoot,
    cacheRoot: '/unused',
    fetchPinned,
  })
  assert.equal(report.complete, true, report.error)
  assert.deepEqual(changes.fixtures.added, ['native.auth.new'])
})

test('a changes file that cannot be written still prints valid incomplete JSON', async t => {
  const out = join(await scratch(t, 'voucha-diff-out-'), 'missing-dir', 'changes.json')
  const result = await run(
    process.execPath,
    [script, '--base', 'no-such-revision', '--head', 'HEAD', '--changes', out],
    { cwd: resolve(import.meta.dirname, '..') },
  ).catch(error => error)
  assert.equal(result.code, 1)
  const report = JSON.parse(result.stdout)
  assert.equal(report.complete, false)
  assert.match(report.error, /failed to write .*missing-dir/)
  assert.equal(report.changes, undefined)
})
