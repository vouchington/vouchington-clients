import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'

const workflow = readFileSync(
  new URL('../.github/workflows/dependabot-swift-android-repair.yml', import.meta.url),
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
  {
    failCommitFetch = false,
    failPullRequestFetch = false,
    failFilesFetch = false,
    pullRequestChanges = {},
    files = [{ filename: 'swift-clients/apps/android/Package.resolved', previous_filename: null }],
  } = {},
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
        changed_files: files.length,
        ...pullRequestChanges,
      }),
    )
    writeFileSync(join(root, 'commit.json'), JSON.stringify(commit))
    writeFileSync(join(root, 'files.jsonl'), files.map(file => JSON.stringify(file)).join('\n'))
    writeFileSync(join(root, 'output'), '')
    const stub = `gh() {
      case "$2" in
        repos/test/native/pulls/402)
          [ "$FAIL_PR_FETCH" = false ] || return 21
          cat "$RUNNER_TEMP/pr.json" ;;
        repos/test/native/commits/${sha})
          [ "$FAIL_COMMIT_FETCH" = false ] || return 22
          cat "$RUNNER_TEMP/commit.json" ;;
        --paginate)
          case "$3" in
            repos/test/native/pulls/402/files*)
              [ "$FAIL_FILES_FETCH" = false ] || return 23
              cat "$RUNNER_TEMP/files.jsonl" ;;
            *) return 99 ;;
          esac ;;
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
        FAIL_FILES_FETCH: String(failFilesFetch),
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

  for (const [label, files] of [
    ['a changed workflow file', [{ filename: '.github/workflows/validate.yml' }]],
    ['a changed action file', [{ filename: '.github/actions/x/action.yml' }]],
    [
      'a file renamed away from .github',
      [{ filename: 'docs/moved.yml', previous_filename: '.github/workflows/old.yml' }],
    ],
  ]) {
    it(`refuses repair when the pull request has ${label}`, () => {
      const result = invoke(commit(), { files })
      assert.equal(result.status, 0, result.stderr)
      assert.equal(result.output.trim().split('\n').at(-1), 'eligible=false')
    })
  }

  it('fails closed when the changed-file listing errors or is truncated', () => {
    const failed = invoke(commit(), { failFilesFetch: true })
    assert.notEqual(failed.status, 0)
    assert.equal(failed.output.trim().split('\n').at(-1), 'eligible=false')
    const truncated = invoke(commit(), { pullRequestChanges: { changed_files: 5 } })
    assert.notEqual(truncated.status, 0)
    assert.equal(truncated.output.trim().split('\n').at(-1), 'eligible=false')
  })

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

  it('gates every privileged preparation step without weakening metadata trust', () => {
    const laterSteps = preparation.split('      - name:').slice(2)
    assert.ok(laterSteps.length > 5)
    for (const step of laterSteps) {
      assert.match(step, /if:.*steps\.live\.outputs\.eligible == 'true'/u)
    }
    assert.match(workflow, /eligible: \$\{\{ steps\.live\.outputs\.eligible \}\}/u)
    assert.match(workflow, /uses: dependabot\/fetch-metadata@[a-f0-9]{40}/u)
    assert.doesNotMatch(preparation, /skip-verification|continue-on-error/u)
    assert.doesNotMatch(workflow, /auto-?merge:|--disable-auto|dependabot-automerge/u)
  })
})
