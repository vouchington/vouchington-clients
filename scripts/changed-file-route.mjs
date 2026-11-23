import { appendFileSync } from 'node:fs'
import { execFileSync } from 'node:child_process'

const fullCommitSha = /^[a-f0-9]{40}$/u

export function matchesAnyChangedPath(paths, prefixes) {
  return paths.some(path =>
    prefixes.some(prefix => (prefix.endsWith('/') ? path.startsWith(prefix) : path === prefix)),
  )
}

export function requireFullCommitSha(value, label) {
  if (!fullCommitSha.test(value ?? '')) throw new Error(`${label} must be a full commit SHA`)
  return value
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

function writeRoute(logLabel, outputKey, selected, note = '') {
  console.log(`${logLabel}: ${outputKey}=${selected}${note}`)
  if (process.env.GITHUB_OUTPUT) {
    appendFileSync(process.env.GITHUB_OUTPUT, `${outputKey}=${selected}\n`)
  }
}

// `mergeGroupFallback` is the output written when a merge group's whole-ref diff cannot be
// computed; it must be the value that runs the guarded jobs.
export function runChangedFileRoute({
  repositoryRoot,
  select,
  logLabel,
  outputKey,
  mergeGroupFallback = true,
  eventName = process.env.EVENT_NAME,
  argv = process.argv,
}) {
  const [, , baseArgument, revisionArgument] = argv
  const base = requireFullCommitSha(baseArgument, 'base SHA')
  const revision = requireFullCommitSha(revisionArgument, 'revision SHA')
  if (eventName === 'merge_group') {
    let changed
    try {
      changed = mergeGroupChangedPaths({ repositoryRoot, revision })
    } catch (error) {
      writeRoute(logLabel, outputKey, mergeGroupFallback, ` (diff unavailable: ${error.message})`)
      return
    }
    writeRoute(logLabel, outputKey, select(changed))
    return
  }
  execFileSync('git', ['fetch', '--no-tags', '--depth=1', 'origin', base], {
    cwd: repositoryRoot,
    stdio: 'inherit',
  })
  const changed = execFileSync(
    'git',
    ['diff', '--no-renames', '--name-only', '-z', base, revision],
    {
      cwd: repositoryRoot,
      encoding: 'utf8',
    },
  )
    .split('\0')
    .filter(Boolean)
  writeRoute(logLabel, outputKey, select(changed))
}
