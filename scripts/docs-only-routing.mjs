import { appendFileSync } from 'node:fs'
import { execFileSync } from 'node:child_process'
import { resolve } from 'node:path'
import { requireFullCommitSha, runChangedFileRoute } from './changed-file-route.mjs'

// A change is docs-only when it touches at least one path and every path is Markdown or under docs/.
// No native job reads either (verified by grep over Swift, .NET, scripts and actions), so such a
// change cannot alter a native build, test, or smoke result.
export function isDocsOnly(paths) {
  return paths.length > 0 && paths.every(path => path.endsWith('.md') || path.startsWith('docs/'))
}

const defaultGit = (repositoryRoot, args) =>
  execFileSync('git', args, {
    cwd: repositoryRoot,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'inherit'],
  })

// A merge-group entry's base_sha is the previous entry's group commit, so base_sha..head_sha is
// only that entry's diff. The queue is the last gate, so route on everything the queued ref adds
// over main: merge-base(origin/main, revision)..revision.
export function mergeGroupChangedPaths({ repositoryRoot, revision, git = defaultGit }) {
  requireFullCommitSha(revision, 'revision SHA')
  git(repositoryRoot, [
    'fetch',
    '--no-tags',
    '--depth=100',
    'origin',
    '+refs/heads/main:refs/remotes/origin/main',
  ])
  git(repositoryRoot, ['fetch', '--no-tags', '--depth=100', 'origin', revision])
  const base = git(repositoryRoot, ['merge-base', 'origin/main', revision]).trim()
  requireFullCommitSha(base, 'merge base')
  return git(repositoryRoot, ['diff', '--no-renames', '--name-only', '-z', base, revision])
    .split('\0')
    .filter(Boolean)
}

function runMergeGroupRoute(repositoryRoot, revision) {
  const docsOnly = isDocsOnly(mergeGroupChangedPaths({ repositoryRoot, revision }))
  console.log(`Docs-only changed-file route: ${docsOnly ? 'skip' : 'run'}`)
  if (process.env.GITHUB_OUTPUT)
    appendFileSync(process.env.GITHUB_OUTPUT, `docs-only=${docsOnly}\n`)
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(import.meta.filename)) {
  const repositoryRoot = resolve(import.meta.dirname, '..')
  try {
    if (process.env.EVENT_NAME === 'merge_group')
      runMergeGroupRoute(repositoryRoot, process.argv[3])
    else
      runChangedFileRoute({
        repositoryRoot,
        select: isDocsOnly,
        logLabel: 'Docs-only changed-file route',
        outputKey: 'docs-only',
      })
  } catch (error) {
    // Fail safe: an uncomputable diff (shallow fetch, missing base) runs the full suite.
    console.log(`Docs-only changed-file route: run (diff unavailable: ${error.message})`)
    if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, 'docs-only=false\n')
  }
}
