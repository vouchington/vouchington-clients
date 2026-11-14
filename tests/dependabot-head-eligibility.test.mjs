import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'

const workflow = readFileSync(
  new URL('../.github/workflows/dependabot-automerge.yml', import.meta.url),
  'utf8',
)
const preparation = workflow.split('\n  publish-swift-android:')[0]
const liveStep = preparation
  .split('      - name: Capture live Dependabot identity')[1]
  .split('\n      - name:')[0]
const run = liveStep
  .split('        run: |\n')[1]
  .split('\n')
  .map(line => line.replace(/^ {10}/u, ''))
  .join('\n')
const sha = 'b'.repeat(40)

function invoke(
  commit,
  { failCommitFetch = false, failPullRequestFetch = false, pullRequestChanges = {} } = {},
) {
  const root = mkdtempSync(join(tmpdir(), 'native-dependabot-head-'))
  try {
    writeFileSync(
      join(root, 'pr.json'),
      JSON.stringify({
        state: 'open',
        draft: false,
        user: { login: 'dependabot[bot]' },
        base: { ref: 'main', sha: 'a'.repeat(40) },
        head: { ref: 'dependabot/swift/crypto', sha, repo: { full_name: 'test/native' } },
        ...pullRequestChanges,
      }),
    )
    writeFileSync(join(root, 'commit.json'), JSON.stringify(commit))
    writeFileSync(join(root, 'output'), '')
    const stub = `gh() {
      case "$2" in
        repos/test/native/pulls/402)
          [ "$FAIL_PR_FETCH" = false ] || return 21
          cat "$RUNNER_TEMP/pr.json" ;;
        repos/test/native/commits/${sha})
          [ "$FAIL_COMMIT_FETCH" = false ] || return 22
          cat "$RUNNER_TEMP/commit.json" ;;
        *) return 99 ;;
      esac
    }
    `
    const result = spawnSync('bash', ['-c', stub + run], {
      encoding: 'utf8',
      env: {
        ...process.env,
        RUNNER_TEMP: root,
        GITHUB_OUTPUT: join(root, 'output'),
        GITHUB_REPOSITORY: 'test/native',
        DEFAULT_BRANCH: 'main',
        PR_NUMBER: '402',
        FAIL_COMMIT_FETCH: String(failCommitFetch),
        FAIL_PR_FETCH: String(failPullRequestFetch),
      },
    })
    return { ...result, output: readFileSync(join(root, 'output'), 'utf8') }
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

function commit(login = 'dependabot[bot]', verified = true, committer = 'web-flow') {
  return {
    sha,
    author: login === null ? null : { login },
    committer: { login: committer },
    commit: { verification: { verified } },
  }
}

describe('trusted Dependabot head eligibility', () => {
  it('admits a verified bot head to the existing metadata verification', () => {
    const result = invoke(commit())
    assert.equal(result.status, 0, result.stderr)
    assert.equal(result.output.trim().split('\n').at(-1), 'eligible=true')
    assert.match(result.output, new RegExp(`^head-sha=${sha}$`, 'mu'))
  })

  for (const [label, candidate] of [
    ['signed maintainer repair', commit('maintainer')],
    ['unsigned maintainer repair', commit('maintainer', false)],
    ['unsigned automated repair', commit('dependabot[bot]', false)],
    ['unassociated commit author', commit(null)],
    ['unassociated commit committer', { ...commit(), committer: null }],
    ['verified bot author with human committer', commit('dependabot[bot]', true, 'maintainer')],
  ]) {
    it(`leaves a ${label} to ordinary reviewed CI`, () => {
      const result = invoke(candidate)
      assert.equal(result.status, 0, result.stderr)
      assert.equal(result.output.trim().split('\n').at(-1), 'eligible=false')
    })
  }

  for (const [label, candidate] of [
    ['wrong commit identity', { ...commit(), sha: 'c'.repeat(40) }],
    ['missing verification', { sha, author: { login: 'dependabot[bot]' } }],
    ['malformed verification', commit('dependabot[bot]', 'true')],
    ['malformed committer', { ...commit(), committer: { login: true } }],
  ]) {
    it(`fails closed for ${label}`, () => {
      const result = invoke(candidate)
      assert.notEqual(result.status, 0)
      assert.equal(result.output.trim().split('\n').at(-1), 'eligible=false')
    })
  }

  it('exposes conservative cleanup identity before a failed commit lookup without passing prepare', () => {
    const result = invoke(commit(), { failCommitFetch: true })
    assert.equal(result.status, 22)
    assert.deepEqual(result.output.trim().split('\n'), [
      `base-sha=${'a'.repeat(40)}`,
      `head-sha=${sha}`,
      'eligible=false',
    ])
  })

  it('does not expose cleanup identity when the initial pull request lookup fails', () => {
    const result = invoke(commit(), { failPullRequestFetch: true })
    assert.equal(result.status, 21)
    assert.equal(result.output, '')
  })

  it('does not expose cleanup identity before the live pull request passes all identity guards', () => {
    const result = invoke(commit(), {
      pullRequestChanges: { base: { ref: 'release', sha: 'a'.repeat(40) } },
    })
    assert.notEqual(result.status, 0)
    assert.equal(result.output, '')
  })

  it('gates every privileged preparation step and auto-merge without weakening metadata trust', () => {
    const laterSteps = preparation.split('      - name:').slice(2)
    assert.ok(laterSteps.length > 5)
    for (const step of laterSteps) {
      assert.match(step, /if:.*steps\.live\.outputs\.eligible == 'true'/u)
    }
    assert.match(workflow, /eligible: \$\{\{ steps\.live\.outputs\.eligible \}\}/u)
    assert.match(workflow, /needs\.prepare\.outputs\.eligible == 'true'/u)
    assert.match(workflow, /uses: dependabot\/fetch-metadata@[a-f0-9]{40}/u)
    assert.doesNotMatch(preparation, /skip-verification|continue-on-error/u)
    const cleanup = workflow
      .split('\n  cleanup-retargeted-automerge:')[1]
      .split('\n  cleanup-ineligible-automerge:')[0]
    assert.match(cleanup, /base\.ref != github\.event\.repository\.default_branch/u)
    assert.match(cleanup, /Disable stale Dependabot auto-merge/u)
  })

  it('disables inherited auto-merge only for the freshly verified ineligible PR head', () => {
    const job = workflow.split('\n  cleanup-ineligible-automerge:\n')[1]
    assert.ok(job)
    assert.match(job, /needs: prepare/u)
    assert.match(job, /always\(\) && needs\.prepare\.result != 'cancelled'/u)
    assert.match(job, /needs\.prepare\.outputs\.eligible == 'false'/u)
    assert.match(job, /EXPECTED_HEAD_SHA: \$\{\{ needs\.prepare\.outputs\.head-sha \}\}/u)
    assert.doesNotMatch(job, /EXPECTED_BASE_SHA|\.base\.sha/u)
    assert.match(job, /\.base\.ref == \$default/u)
    assert.match(job, /\.head\.sha == \$head/u)
    assert.match(job, /--disable-auto\n/u)
    assert.doesNotMatch(job, /--match-head-commit/u)
    assert.match(job, /has\("auto_merge"\)/u)
    assert.match(job, /\.auto_merge == null/u)
    assert.match(job, /GH_TOKEN: \$\{\{ secrets\.DEPENDABOT_AUTOMERGE_TOKEN \}\}/u)

    const run = job
      .split('        run: |\n')[1]
      .split('\n')
      .map(line => line.replace(/^ {10}/u, ''))
      .join('\n')
    const root = mkdtempSync(join(tmpdir(), 'native-dependabot-cleanup-'))
    try {
      const pullRequest = {
        state: 'open',
        draft: false,
        number: 402,
        user: { login: 'dependabot[bot]' },
        base: { ref: 'main', sha: 'a'.repeat(40) },
        head: {
          ref: 'dependabot/swift/crypto',
          sha,
          repo: { full_name: 'test/native' },
        },
        auto_merge: {
          enabled_by: { login: 'dependabot[bot]' },
          merge_method: 'squash',
          commit_title: null,
          commit_message: null,
        },
      }
      writeFileSync(join(root, 'pr.json'), JSON.stringify(pullRequest))
      const stub = `gh() {
        case "$1" in
          api) cat "$RUNNER_TEMP/pr.json" ;;
          pr) printf '%s\\n' "$@" > "$RUNNER_TEMP/merge-args" ;;
          *) return 99 ;;
        esac
      }
      `
      const invokeCleanup = () =>
        spawnSync('bash', ['-c', stub + run], {
          encoding: 'utf8',
          env: {
            ...process.env,
            AUTOMERGE_TOKEN: 'test-token',
            DEFAULT_BRANCH: 'main',
            EXPECTED_HEAD_SHA: sha,
            GH_TOKEN: 'test-token',
            PR_NUMBER: '402',
            REPOSITORY: 'test/native',
            RUNNER_TEMP: root,
          },
        })

      const current = invokeCleanup()
      assert.equal(current.status, 0, current.stderr)
      assert.equal(
        readFileSync(join(root, 'merge-args'), 'utf8').trim(),
        ['pr', 'merge', '402', '--repo', 'test/native', '--disable-auto'].join('\n'),
      )

      pullRequest.base.sha = 'b'.repeat(40)
      writeFileSync(join(root, 'pr.json'), JSON.stringify(pullRequest))
      writeFileSync(join(root, 'merge-args'), '')
      const afterBaseAdvance = invokeCleanup()
      assert.equal(afterBaseAdvance.status, 0, afterBaseAdvance.stderr)
      assert.equal(
        readFileSync(join(root, 'merge-args'), 'utf8').trim().split('\n').at(-1),
        '--disable-auto',
      )

      const alreadyDisabled = structuredClone(pullRequest)
      alreadyDisabled.auto_merge = null
      writeFileSync(join(root, 'pr.json'), JSON.stringify(alreadyDisabled))
      writeFileSync(join(root, 'merge-args'), '')
      const idempotent = invokeCleanup()
      assert.equal(idempotent.status, 0, idempotent.stderr)
      assert.equal(readFileSync(join(root, 'merge-args'), 'utf8'), '')

      for (const malformed of [
        Object.fromEntries(Object.entries(pullRequest).filter(([key]) => key !== 'auto_merge')),
        { ...pullRequest, auto_merge: {} },
        { ...pullRequest, auto_merge: { ...pullRequest.auto_merge, merge_method: 'automatic' } },
      ]) {
        writeFileSync(join(root, 'pr.json'), JSON.stringify(malformed))
        writeFileSync(join(root, 'merge-args'), '')
        const rejected = invokeCleanup()
        assert.notEqual(rejected.status, 0)
        assert.equal(readFileSync(join(root, 'merge-args'), 'utf8'), '')
      }

      for (const [label, change] of [
        [
          'changed head',
          pr => {
            pr.head.sha = 'c'.repeat(40)
          },
        ],
        [
          'retargeted PR',
          pr => {
            pr.base.ref = 'release'
          },
        ],
        [
          'closed PR',
          pr => {
            pr.state = 'closed'
          },
        ],
      ]) {
        const candidate = structuredClone(pullRequest)
        change(candidate)
        writeFileSync(join(root, 'pr.json'), JSON.stringify(candidate))
        writeFileSync(join(root, 'merge-args'), '')
        const stale = invokeCleanup()
        assert.notEqual(stale.status, 0, label)
        assert.equal(readFileSync(join(root, 'merge-args'), 'utf8'), '', label)
      }
    } finally {
      rmSync(root, { recursive: true, force: true })
    }
  })
})
