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

export function runChangedFileRoute({
  repositoryRoot,
  select,
  logLabel,
  outputKey,
  argv = process.argv,
}) {
  const [, , baseArgument, revisionArgument] = argv
  const base = requireFullCommitSha(baseArgument, 'base SHA')
  const revision = requireFullCommitSha(revisionArgument, 'revision SHA')
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
  const selected = select(changed)
  console.log(`${logLabel}: ${selected ? 'run' : 'skip'}`)
  if (process.env.GITHUB_OUTPUT) {
    appendFileSync(process.env.GITHUB_OUTPUT, `${outputKey}=${selected}\n`)
  }
}
