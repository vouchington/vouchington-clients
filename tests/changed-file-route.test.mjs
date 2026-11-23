import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
import {
  matchesAnyChangedPath,
  requireFullCommitSha,
  runChangedFileRoute,
} from '../scripts/changed-file-route.mjs'
import { shouldRunWindowsImagePreviewTests } from '../scripts/windows-image-preview-test-routing.mjs'
import { shouldRunWindowsLanguageModelSurfaceProbe } from '../scripts/windows-language-model-surface-routing.mjs'
import { shouldRunWindowsMauiBuild } from '../scripts/windows-maui-test-routing.mjs'
import { mergeQueueFixture } from './merge-queue-fixture.mjs'
import { jobBlock } from './workflow-job-block.mjs'

const native = await readFile(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)

// Runs a Windows MAUI route as the verify job would, with an isolated GITHUB_OUTPUT.
function routeWindowsMaui({ repositoryRoot, base, revision, eventName, mergeGroupFallback }) {
  const output = join(mkdtempSync(join(tmpdir(), 'changed-file-route-')), 'output')
  const previous = process.env.GITHUB_OUTPUT
  process.env.GITHUB_OUTPUT = output
  try {
    runChangedFileRoute({
      argv: ['node', 'script', base, revision],
      eventName,
      repositoryRoot,
      select: shouldRunWindowsMauiBuild,
      logLabel: 'Windows MAUI build changed-file route',
      outputKey: 'windows-maui',
      ...(mergeGroupFallback === undefined ? {} : { mergeGroupFallback }),
    })
  } finally {
    if (previous === undefined) delete process.env.GITHUB_OUTPUT
    else process.env.GITHUB_OUTPUT = previous
  }
  return readFileSync(output, 'utf8')
}

describe('changed-file route helper', () => {
  it('matches a directory prefix and an exact file, not a longer sibling name', () => {
    assert.equal(
      matchesAnyChangedPath(['dotnet-clients/src/App.cs'], ['dotnet-clients/src/']),
      true,
    )
    assert.equal(
      matchesAnyChangedPath(['dotnet-clients/src-extra/App.cs'], ['dotnet-clients/src/']),
      false,
    )
    assert.equal(
      matchesAnyChangedPath(
        ['scripts/windows-maui-test-routing.mjs'],
        ['scripts/windows-maui-test-routing.mjs'],
      ),
      true,
    )
    assert.equal(
      matchesAnyChangedPath(
        ['scripts/windows-maui-test-routing.mjs.bak'],
        ['scripts/windows-maui-test-routing.mjs'],
      ),
      false,
    )
  })

  it('accepts only a lowercase 40-character commit SHA', () => {
    const sha = 'a'.repeat(40)
    assert.equal(requireFullCommitSha(sha, 'base SHA'), sha)
    assert.throws(
      () => requireFullCommitSha(sha.toUpperCase(), 'base SHA'),
      /base SHA must be a full commit SHA/u,
    )
    assert.throws(
      () => requireFullCommitSha('abc', 'revision SHA'),
      /revision SHA must be a full commit SHA/u,
    )
    assert.throws(
      () => requireFullCommitSha(undefined, 'base SHA'),
      /base SHA must be a full commit SHA/u,
    )
  })

  it('rejects a short base SHA before invoking git', () => {
    assert.throws(
      () =>
        runChangedFileRoute({
          argv: ['node', 'script', 'abc', 'a'.repeat(40)],
          repositoryRoot: '/does-not-exist',
          select: () => {
            throw new Error('selector must not run')
          },
          logLabel: 'route',
          outputKey: 'example',
        }),
      /base SHA must be a full commit SHA/u,
    )
  })

  it('selects every Windows job when the shared helper changes', () => {
    assert.equal(shouldRunWindowsMauiBuild(['scripts/changed-file-route.mjs']), true)
    assert.equal(shouldRunWindowsImagePreviewTests(['scripts/changed-file-route.mjs']), true)
    assert.equal(
      shouldRunWindowsLanguageModelSurfaceProbe(['scripts/changed-file-route.mjs']),
      true,
    )
  })

  it('routes a merge group on the whole queued ref, not the entry diff', () => {
    const queue = mergeQueueFixture('windows-queue-')
    queue.run('checkout', '-q', '-b', 'queue')
    const windowsEntry = queue.commit('dotnet-clients/src/App.cs', 'windows entry')
    // The second entry's merge_group.base_sha is the Windows entry's group commit.
    const notesEntry = queue.commit('NOTES.md', 'notes entry')
    queue.run('push', '-q', 'origin', 'queue')
    assert.equal(shouldRunWindowsMauiBuild(['NOTES.md']), false, 'entry diff alone would skip')
    const route = eventName =>
      routeWindowsMaui({
        repositoryRoot: queue.consumer('queue'),
        base: windowsEntry,
        revision: notesEntry,
        eventName,
        // A false fallback makes this pass only when the whole-ref diff succeeds.
        mergeGroupFallback: false,
      })
    assert.equal(route('merge_group'), 'windows-maui=true\n')
    assert.equal(route('pull_request'), 'windows-maui=false\n', 'pull requests keep base..head')
  })

  it('fails safe to running Windows when a merge group diff cannot be computed', () => {
    const queue = mergeQueueFixture('windows-queue-missing-')
    const output = routeWindowsMaui({
      repositoryRoot: queue.consumer(),
      base: queue.base,
      revision: 'b'.repeat(40),
      eventName: 'merge_group',
    })
    assert.equal(output, 'windows-maui=true\n')
  })

  it('passes the event name to every Windows route step', () => {
    const verify = jobBlock(native, 'verify')
    for (const script of [
      'windows-image-preview-test-routing.mjs',
      'windows-maui-test-routing.mjs',
      'windows-language-model-surface-routing.mjs',
    ]) {
      const run = `run: node scripts/${script} "$BASE_SHA" "$REVISION_SHA"`
      const body = verify
        .slice(0, verify.indexOf(run))
        .split(/\n {6}- name:/u)
        .pop()
      assert.ok(verify.includes(run), script)
      assert.ok(body.includes('EVENT_NAME: ${{ github.event_name }}'), script)
      assert.ok(body.includes('BASE_SHA: ${{ steps.identity.outputs.base_sha }}'), script)
    }
  })
})
