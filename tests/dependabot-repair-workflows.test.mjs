import assert from 'node:assert/strict'
import { access, readFile } from 'node:fs/promises'
import { describe, it } from 'node:test'

const automerge = await readFile(
  new URL('../.github/workflows/dependabot-automerge.yml', import.meta.url),
  'utf8',
)
const dotnet = await readFile(
  new URL('../.github/workflows/repair-dependabot-dotnet-locks.yml', import.meta.url),
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
    assert.match(automerge, /\.dependencyName == "source\.skip\.tools\/skip"/u)
    assert.doesNotMatch(automerge, /outputs\.dependency-names/u)
  })

  it('never checks out or executes a pull-request tree', () => {
    for (const workflow of [automerge, dotnet]) {
      assert.doesNotMatch(workflow, /ref:\s*\$\{\{\s*github\.event\.pull_request\.head/u)
      assert.doesNotMatch(workflow, /git (?:checkout|switch|worktree)/u)
      assert.match(workflow, /Check out exact trusted base/u)
      assert.match(workflow, /persist-credentials: false/u)
    }
    assert.match(automerge, /Fetch candidate files as inert data/u)
  })

  it('binds artifacts and repair commits to exact live inputs', () => {
    for (const workflow of [automerge, dotnet]) {
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

  it('exposes the write token only to final push or pinned auto-merge steps', () => {
    assert.equal(count(dotnet, /secrets\.DEPENDABOT_AUTOMERGE_TOKEN/gu), 1)
    assert.equal(count(automerge, /secrets\.DEPENDABOT_AUTOMERGE_TOKEN/gu), 3)
    assert.equal(count(dotnet, /name: Push with a single-command write credential/gu), 1)
    assert.equal(count(automerge, /name: Push with a single-command write credential/gu), 1)
  })

  it('repairs only the established fixed output sets', () => {
    assert.match(automerge, /published-paths/u)
    assert.match(automerge, /\$\(find repair-artifact -type f \| wc -l[^\n]+== 2/u)
    assert.match(dotnet, /published-paths/u)
    assert.match(dotnet, /\$\(find repair-artifact -type f \| wc -l[^\n]+== 8/u)
    assert.match(dotnet, /group: dependabot-dotnet-lock-repair-\$\{\{ github\.event\.pull_request\.number \}\}/u)
    assert.match(automerge, /expected_base_sha:/u)
    assert.match(automerge, /expected_head_sha:/u)
  })
})
