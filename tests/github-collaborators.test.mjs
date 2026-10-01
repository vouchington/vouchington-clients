import assert from 'node:assert/strict'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  classifyAuthors,
  isTrustedPermission,
  runCollaboratorsCli,
} from '../scripts/github-collaborators.mjs'

const permissions = { alice: 'admin', bob: 'write', carol: 'read', dave: 'none' }
const fetchPermission = login => permissions[login] ?? 'none'

function withAuthorsFile(authors, run) {
  const dir = mkdtempSync(join(tmpdir(), 'github-collaborators-'))
  const path = join(dir, 'authors.json')
  writeFileSync(path, JSON.stringify(authors))
  try {
    return run(path)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}

test('trusts only admin and write permissions', () => {
  assert.equal(isTrustedPermission('admin'), true)
  assert.equal(isTrustedPermission('write'), true)
  for (const permission of ['maintain', 'triage', 'read', 'none', '', undefined]) {
    assert.equal(isTrustedPermission(permission), false, String(permission))
  }
})

test('classifies each distinct author once', () => {
  const lookups = []
  const result = classifyAuthors(
    [
      { login: 'alice', type: 'User' },
      { login: 'carol', type: 'User' },
      { login: 'bob', type: 'User' },
      { login: 'alice', type: 'User' },
      { login: 'dave', type: 'User' },
    ],
    {
      fetchPermission(login) {
        lookups.push(login)
        return fetchPermission(login)
      },
    },
  )
  assert.deepEqual(result, { trusted: ['alice', 'bob'], untrusted: ['carol', 'dave'] })
  assert.deepEqual(lookups, ['alice', 'carol', 'bob', 'dave'])
})

test('treats bots as untrusted unless explicitly allowed, without a permission lookup', () => {
  const authors = [{ login: 'github-actions[bot]', type: 'Bot' }]
  const noLookup = () => assert.fail('bots must not be looked up')
  assert.deepEqual(classifyAuthors(authors, { fetchPermission: noLookup }), {
    trusted: [],
    untrusted: ['github-actions[bot]'],
  })
  assert.deepEqual(classifyAuthors(authors, { allowBots: true, fetchPermission: noLookup }), {
    trusted: ['github-actions[bot]'],
    untrusted: [],
  })
})

test('fails closed on lookup errors and malformed input', () => {
  assert.throws(
    () =>
      classifyAuthors([{ login: 'alice', type: 'User' }], {
        fetchPermission() {
          throw new Error('HTTP 502')
        },
      }),
    /HTTP 502/u,
  )
  assert.throws(() => classifyAuthors({}, { fetchPermission }), /must be an array/u)
  assert.throws(() => classifyAuthors([{ type: 'User' }], { fetchPermission }), /login is missing/u)
})

test('CLI classifies authors from a file against the named repository', () => {
  const seen = []
  const output = withAuthorsFile(
    [
      { login: 'bob', type: 'User' },
      { login: 'carol', type: 'User' },
      { login: 'sourcery-ai[bot]', type: 'Bot' },
    ],
    path =>
      runCollaboratorsCli(
        ['node', 'cli', 'vouchington/vouchington-clients', path, '--allow-bots'],
        (repo, login) => {
          seen.push(repo)
          return fetchPermission(login)
        },
      ),
  )
  assert.deepEqual(JSON.parse(output), {
    trusted: ['bob', 'sourcery-ai[bot]'],
    untrusted: ['carol'],
  })
  assert.deepEqual(new Set(seen), new Set(['vouchington/vouchington-clients']))
})

test('CLI rejects bad arguments', () => {
  assert.throws(() => runCollaboratorsCli(['node', 'cli']), /Usage/u)
  assert.throws(() => runCollaboratorsCli(['node', 'cli', '../etc', 'x.json']), /Usage/u)
  assert.throws(() => runCollaboratorsCli(['node', 'cli', 'o/r', 'x.json', '--everyone']), /Usage/u)
})
