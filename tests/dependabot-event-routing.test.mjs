import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'

const workflow = name =>
  readFileSync(new URL(`../.github/workflows/${name}`, import.meta.url), 'utf8')
const main = workflow('dependabot-automerge.yml')
const edited = workflow('dependabot-edited.yml')
const directTypes = main.match(/pull_request_target:\n    types: \[(.*?)\]/u)[1].split(', ')
const editedTypes = edited.match(/pull_request_target:\n    types: \[(.*?)\]/u)[1].split(', ')
const predicate = edited.match(/    if: >-\n([\s\S]*?)    uses:/u)[1].trim()
const identity = {
  user: { login: 'dependabot[bot]' },
  base: { ref: 'main' },
  head: { ref: 'dependabot/npm_and_yarn/dependencies', repo: { full_name: 'test/native' } },
}

// Evaluate only the actual router's three supported identity clauses. These are
// wiring controls; GitHub owns reusable-call expansion and skipped check records.
function accepts(pr) {
  const clauses = {
    "github.event.pull_request.user.login == 'dependabot[bot]'":
      pr.user.login === 'dependabot[bot]',
    'github.event.pull_request.head.repo.full_name == github.repository':
      pr.head.repo.full_name === 'test/native',
    "startsWith(github.event.pull_request.head.ref, 'dependabot/')":
      pr.head.ref.startsWith('dependabot/'),
  }
  return predicate.split(/\s*&&\s*/u).every(clause => {
    assert.ok(Object.hasOwn(clauses, clause), `unsupported router clause: ${clause}`)
    return clauses[clause]
  })
}

describe('Dependabot metadata event routing', () => {
  it('routes only edited events through one conditional reusable caller', () => {
    assert.deepEqual(editedTypes, ['edited'])
    assert.deepEqual(
      [...edited.matchAll(/^  ([\w-]+):$/gmu)].slice(-1).map(m => m[1]),
      ['dependabot'],
    )
    const jobs = edited.split('\njobs:\n')[1]
    assert.equal([...jobs.matchAll(/^  [\w-]+:$/gmu)].length, 1)
    assert.match(jobs, /uses: \.\/\.github\/workflows\/dependabot-automerge\.yml/u)
    assert.doesNotMatch(jobs, /steps:|strategy:|needs:|runs-on:/u)
    assert.doesNotMatch(edited, /github\.actor|concurrency:/u)
  })

  for (const field of ['title', 'body', 'Shepherd Journal body']) {
    it(`rejects ordinary human ${field} edits before calling the worker`, () => {
      assert.equal(accepts({ ...identity, user: { login: 'maintainer' } }), false)
      assert.equal(directTypes.includes('edited'), false)
    })
  }

  it('admits valid Dependabot metadata and retarget edits regardless of editing actor', () => {
    assert.equal(accepts(identity), true)
    assert.equal(accepts({ ...identity, base: { ref: 'release/other' } }), true)
    assert.match(main, /base\.ref == github\.event\.repository\.default_branch/u)
    assert.match(main, /base\.ref != github\.event\.repository\.default_branch/u)
    assert.match(main, /cleanup-retargeted-automerge:/u)
    assert.match(main, /Fetch trusted Dependabot metadata/u)
  })

  for (const action of ['opened', 'reopened', 'ready_for_review', 'synchronize']) {
    it(`keeps ${action} on the original worker only`, () => {
      assert.equal(directTypes.includes(action), true)
      assert.equal(editedTypes.includes(action), false)
    })
  }

  it('rejects spoofed author, foreign repository and non-Dependabot head branches', () => {
    assert.equal(accepts({ ...identity, user: { login: 'dependabot' } }), false)
    assert.equal(
      accepts({ ...identity, head: { ...identity.head, repo: { full_name: 'fork/native' } } }),
      false,
    )
    assert.equal(accepts({ ...identity, head: { ...identity.head, ref: 'repair/deps' } }), false)
  })

  it('passes only the existing write secret and sufficient read permissions to unchanged jobs', () => {
    assert.match(
      main,
      /workflow_call:\n    secrets:\n      DEPENDABOT_AUTOMERGE_TOKEN:\n        required: true/u,
    )
    assert.match(
      edited,
      /secrets:\n      DEPENDABOT_AUTOMERGE_TOKEN: \$\{\{ secrets\.DEPENDABOT_AUTOMERGE_TOKEN \}\}/u,
    )
    assert.doesNotMatch(edited, /secrets: inherit|: write/u)
    assert.match(edited, /actions: read/u)
    assert.match(edited, /contents: read/u)
    assert.match(edited, /pull-requests: read/u)
    assert.match(
      main,
      /group: Dependabot Auto-merge-\$\{\{ github\.event\.pull_request\.number \|\| github\.sha \}\}/u,
    )
  })
})
