import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { access, readFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { NUGET_LOCK_PATHS } from '../scripts/validate-dependabot-dotnet-repair.mjs'

const swiftAndroid = await readFile(
  new URL('../.github/workflows/dependabot-swift-android-repair.yml', import.meta.url),
  'utf8',
)
const dotnet = await readFile(
  new URL('../.github/workflows/repair-dependabot-dotnet-locks.yml', import.meta.url),
  'utf8',
)
const swiftAndroidClassifier = await readFile(
  new URL('../scripts/classify-dependabot-swift-android-repair.mjs', import.meta.url),
  'utf8',
)

function count(source, pattern) {
  return [...source.matchAll(pattern)].length
}

describe('Dependabot repair workflow contracts', () => {
  it('keeps one authoritative repair path per native ecosystem', async () => {
    await assert.rejects(
      access(new URL('../.github/workflows/repair-dependabot-native.yml', import.meta.url)),
    )
    assert.match(swiftAndroidClassifier, /dependencyName === 'source\.skip\.tools\/skip'/u)
    assert.doesNotMatch(swiftAndroid, /outputs\.dependency-names/u)
  })

  it('runs on pull_request with read-only token permissions and a same-repo Dependabot gate', () => {
    for (const workflow of [swiftAndroid, dotnet]) {
      assert.match(workflow, /\non:\n {2}pull_request:\n {4}types: \[/u)
      assert.doesNotMatch(workflow, /pull_request_target/u)
      assert.doesNotMatch(workflow, /contents: write|pull-requests: write/u)
      for (const job of workflow.split(/\n {2}(?=[a-z-]+:\n {4}(?:needs|if|runs-on):)/u).slice(1)) {
        assert.match(job, /github\.actor == 'dependabot\[bot\]'/u)
        assert.match(job, /github\.event\.pull_request\.user\.login == 'dependabot\[bot\]'/u)
        assert.match(
          job,
          /github\.event\.pull_request\.head\.repo\.full_name == github\.repository/u,
        )
      }
    }
  })

  it('never checks out or executes a pull-request tree', () => {
    for (const workflow of [swiftAndroid, dotnet]) {
      assert.doesNotMatch(workflow, /ref:\s*\$\{\{\s*github\.event\.pull_request\.head/u)
      assert.doesNotMatch(workflow, /git (?:checkout|switch|worktree)/u)
      assert.match(workflow, /Check out exact trusted base/u)
      assert.match(workflow, /persist-credentials: false/u)
    }
    assert.match(swiftAndroid, /Fetch candidate files as inert data/u)
  })

  it('binds artifacts and repair commits to exact live inputs', () => {
    for (const workflow of [swiftAndroid, dotnet]) {
      assert.match(workflow, /git diff --raw --full-index -z --find-renames/u)
      assert.match(workflow, /BASE_SHA\.\.\.\$EXPECTED_HEAD_SHA/u)
      assert.match(workflow, /run-id: \$\{\{ github\.run_id \}\}/u)
      assert.match(workflow, /retention-days: 1/u)
      assert.match(workflow, /if-no-files-found: error/u)
      assert.match(workflow, /git commit-tree/u)
      assert.match(workflow, /git push --force-with-lease=/u)
      assert.match(workflow, /Final live revalidation/u)
    }
  })

  it('exposes the write token only to the final push step', () => {
    assert.equal(count(dotnet, /secrets\.DEPENDABOT_AUTOMERGE_TOKEN/gu), 1)
    assert.equal(count(swiftAndroid, /secrets\.DEPENDABOT_AUTOMERGE_TOKEN/gu), 1)
    assert.equal(count(dotnet, /name: Push with a single-command write credential/gu), 1)
    assert.equal(count(swiftAndroid, /name: Push with a single-command write credential/gu), 1)
  })

  it('repairs only the established fixed output sets', () => {
    assert.match(swiftAndroid, /published-paths/u)
    assert.match(swiftAndroid, /\$\(find repair-artifact -type f \| wc -l[^\n]+== 2/u)
    assert.match(dotnet, /published-paths/u)
    assert.match(
      dotnet,
      /\$\(find repair-artifact -type f \| wc -l[^\n]+\$\(\(\$\{#locks\[@\]\} \+ 1\)\)/u,
    )
    assert.match(
      dotnet,
      /group: dependabot-dotnet-lock-repair-\$\{\{ github\.event\.pull_request\.number \}\}/u,
    )
    assert.doesNotMatch(swiftAndroid, /dependabot-automerge|workflow_call:|--disable-auto/u)
  })

  it('installs the Swift Android publish job without Husky hooks that need mise', () => {
    const publish = swiftAndroid.split('\n  publish-swift-android:')[1]
    assert.match(publish, /HUSKY: '0'\n {8}run: pnpm install --frozen-lockfile/u)
  })

  it('accepts the complete allowed Dependabot change set but rejects larger candidates', () => {
    const predicate = dotnet.match(
      /jq -e --arg default "\$DEFAULT_BRANCH" --arg repo "\$GITHUB_REPOSITORY" '([\s\S]*?)'/u,
    )?.[1]
    assert.ok(predicate)
    const maximum = NUGET_LOCK_PATHS.length + 1
    const pullRequest = {
      state: 'open',
      user: { login: 'dependabot[bot]' },
      base: { ref: 'main', sha: 'a'.repeat(40) },
      head: {
        ref: 'dependabot/nuget/native',
        sha: 'b'.repeat(40),
        repo: { full_name: 'test/native' },
      },
    }
    for (const [changedFiles, accepted] of [
      [1, true],
      [maximum, true],
      [maximum + 1, false],
    ]) {
      const invoke = () =>
        execFileSync(
          'jq',
          ['-e', '--arg', 'default', 'main', '--arg', 'repo', 'test/native', predicate],
          {
            input: JSON.stringify({ ...pullRequest, changed_files: changedFiles }),
            stdio: ['pipe', 'pipe', 'pipe'],
          },
        )
      if (accepted) assert.doesNotThrow(invoke)
      else assert.throws(invoke, error => error.status === 1)
    }
  })

  it('requires exactly the allowed lock files plus provenance in the repair artifact', () => {
    const publish = dotnet.slice(dotnet.indexOf('\n  publish:'))
    const lockArray = publish.match(/\n          locks=\(\n([\s\S]*?)\n          \)/u)
    assert.ok(lockArray)
    assert.deepEqual(lockArray[1].trim().split(/\s+/u), NUGET_LOCK_PATHS)
    const guard = publish
      .split('\n')
      .find(line => line.includes('find repair-artifact -type f | wc -l'))
    assert.ok(guard)
    const root = mkdtempSync(join(tmpdir(), 'dotnet-repair-artifact-'))
    try {
      for (const path of [...NUGET_LOCK_PATHS, 'provenance.json']) {
        const file = join(root, 'repair-artifact', path)
        mkdirSync(dirname(file), { recursive: true })
        writeFileSync(file, '{}')
      }
      const invoke = () =>
        execFileSync('bash', ['-c', `${lockArray[0]}\n${guard}`], { cwd: root, stdio: 'pipe' })
      assert.doesNotThrow(invoke)
      const firstLock = join(root, 'repair-artifact', NUGET_LOCK_PATHS[0])
      rmSync(firstLock)
      assert.throws(invoke, error => error.status === 1)
      writeFileSync(firstLock, '{}')
      writeFileSync(join(root, 'repair-artifact', 'unexpected.json'), '{}')
      assert.throws(invoke, error => error.status === 1)
    } finally {
      rmSync(root, { recursive: true, force: true })
    }
  })
})
