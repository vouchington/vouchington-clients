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

function invoke(commit, failCommitFetch = false) {
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
      }),
    )
    writeFileSync(join(root, 'commit.json'), JSON.stringify(commit))
    writeFileSync(join(root, 'output'), '')
    const stub = `gh() {
      case "$2" in
        repos/test/native/pulls/402) cat "$RUNNER_TEMP/pr.json" ;;
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
    assert.match(result.output, /^eligible=true$/mu)
    assert.match(result.output, new RegExp(`^head-sha=${sha}$`, 'mu'))
  })

  for (const [label, candidate] of [
    ['signed maintainer repair', commit('maintainer')],
    ['unsigned maintainer repair', commit('maintainer', false)],
    ['unsigned automated repair', commit('dependabot[bot]', false)],
    ['unassociated commit author', commit(null)],
    ['verified bot author with human committer', commit('dependabot[bot]', true, 'maintainer')],
  ]) {
    it(`leaves a ${label} to ordinary reviewed CI`, () => {
      const result = invoke(candidate)
      assert.equal(result.status, 0, result.stderr)
      assert.match(result.output, /^eligible=false$/mu)
    })
  }

  for (const [label, candidate] of [
    ['wrong commit identity', { ...commit(), sha: 'c'.repeat(40) }],
    ['missing verification', { sha, author: { login: 'dependabot[bot]' } }],
    ['malformed verification', commit('dependabot[bot]', 'true')],
    ['null committer', { ...commit(), committer: null }],
    ['malformed committer', { ...commit(), committer: { login: true } }],
  ]) {
    it(`fails closed for ${label}`, () => {
      const result = invoke(candidate)
      assert.notEqual(result.status, 0)
      assert.doesNotMatch(result.output, /^eligible=/mu)
    })
  }

  it('does not disguise an API failure as ineligibility', () => {
    const result = invoke(commit(), true)
    assert.equal(result.status, 22)
    assert.doesNotMatch(result.output, /^eligible=/mu)
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
    const cleanup = workflow.split('\n  cleanup-retargeted-automerge:')[1]
    assert.match(cleanup, /base\.ref != github\.event\.repository\.default_branch/u)
    assert.match(cleanup, /Disable stale Dependabot auto-merge/u)
  })
})
