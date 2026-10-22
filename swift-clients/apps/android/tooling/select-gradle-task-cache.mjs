import { createHash } from 'node:crypto'
import { appendFileSync } from 'node:fs'
import { isAbsolute } from 'node:path'
import { pathToFileURL } from 'node:url'

const revision = /^[0-9a-f]{40}$/u
const digest = /^[0-9a-f]{64}$/u
const runnerPart = /^[A-Za-z0-9._-]+$/u

export function selectGradleTaskCache(env, path) {
  if (!isAbsolute(path) || /[\r\n\0]/u.test(path)) {
    throw new Error('Gradle task-output cache requires a single absolute path')
  }
  if (!runnerPart.test(env.RUNNER_OS ?? '') || !runnerPart.test(env.RUNNER_ARCH ?? '')) {
    throw new Error('Runner OS and architecture are required for Gradle task-output cache')
  }
  if (!digest.test(env.TASK_CACHE_CONFIG_HASH ?? '') || !revision.test(env.CANDIDATE_REVISION ?? '')) {
    throw new Error('Gradle task-output cache requires verified config and candidate hashes')
  }
  const pathHash = createHash('sha256').update(path).digest('hex')
  const prefix = `${env.RUNNER_OS}-${env.RUNNER_ARCH}-android-gradle-tasks-v1-${pathHash}-${env.TASK_CACHE_CONFIG_HASH}`
  let sourceRevision
  let previousKey
  if (env.GITHUB_EVENT_NAME === 'pull_request') {
    if (!revision.test(env.PR_BASE_REVISION ?? '')) {
      throw new Error('Pull request Gradle cache requires an exact base revision')
    }
    sourceRevision = env.PR_BASE_REVISION
  } else if (env.GITHUB_EVENT_NAME === 'push' && env.GITHUB_REF === 'refs/heads/main') {
    sourceRevision = env.CANDIDATE_REVISION
    const before = env.PREVIOUS_MAIN_REVISION
    if (!revision.test(before ?? '')) {
      throw new Error('Main push Gradle cache requires an exact previous revision')
    }
    if (before !== '0'.repeat(40)) previousKey = `${prefix}-${before}`
  } else {
    throw new Error('Gradle task-output cache only accepts pull requests or main pushes')
  }
  return { path, pathHash, cacheKey: `${prefix}-${sourceRevision}`, previousKey }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [, , path, output] = process.argv
  if (!output) throw new Error('Pass selected Gradle cache path and GITHUB_OUTPUT')
  const selected = selectGradleTaskCache(process.env, path)
  appendFileSync(
    output,
    `path=${selected.path}\npath-hash=${selected.pathHash}\ncache-key=${selected.cacheKey}\nprevious-key=${selected.previousKey ?? ''}\n`,
  )
}
