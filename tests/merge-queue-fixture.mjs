import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

const env = {
  ...process.env,
  GIT_AUTHOR_NAME: 't',
  GIT_AUTHOR_EMAIL: 't@example.com',
  GIT_COMMITTER_NAME: 't',
  GIT_COMMITTER_EMAIL: 't@example.com',
}

export const git = (cwd, ...args) =>
  execFileSync('git', args, {
    cwd,
    env,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  }).trim()

// An origin with one `main` commit and a writable clone. `consumer(branch)` makes a depth-1 clone
// of that branch, the shape a CI checkout of the candidate has before a route fetches anything.
export function mergeQueueFixture(prefix) {
  const root = mkdtempSync(join(tmpdir(), prefix))
  const origin = join(root, 'origin.git')
  const work = join(root, 'work')
  git(root, 'init', '-q', '--bare', '-b', 'main', origin)
  git(root, 'clone', '-q', origin, work)
  const commit = (file, message) => {
    mkdirSync(dirname(join(work, file)), { recursive: true })
    writeFileSync(join(work, file), message)
    git(work, 'add', file)
    git(work, 'commit', '-q', '-m', message)
    return git(work, 'rev-parse', 'HEAD')
  }
  const base = commit('README.md', 'base')
  git(work, 'push', '-q', 'origin', 'HEAD:main')
  let consumers = 0
  const consumer = (branch = 'main') => {
    const path = join(root, `consumer-${consumers++}`)
    git(root, 'clone', '-q', '--depth=1', '--branch', branch, `file://${origin}`, path)
    return path
  }
  return { root, work, base, commit, consumer, run: (...args) => git(work, ...args) }
}
