import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  matchesAnyChangedPath,
  requireFullCommitSha,
  runChangedFileRoute,
} from '../scripts/changed-file-route.mjs'
import { shouldRunWindowsImagePreviewTests } from '../scripts/windows-image-preview-test-routing.mjs'
import { shouldRunWindowsMauiBuild } from '../scripts/windows-maui-test-routing.mjs'

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

  it('selects both Windows jobs when the shared helper changes', () => {
    assert.equal(shouldRunWindowsMauiBuild(['scripts/changed-file-route.mjs']), true)
    assert.equal(shouldRunWindowsImagePreviewTests(['scripts/changed-file-route.mjs']), true)
  })
})
