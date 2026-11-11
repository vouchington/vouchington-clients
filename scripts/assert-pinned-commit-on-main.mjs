import { execFileSync } from 'node:child_process'
import { isAbsolute } from 'node:path'
import { fileURLToPath } from 'node:url'
import { readContractConfig } from './contract-config.mjs'
import { prefixedFailure } from './prefixed-failure.mjs'

const fail = prefixedFailure('Pinned contract commit rejected')

// compare/<pin>...main reports "ahead" when main is strictly ahead of the pin and "identical"
// when they are the same commit; "behind" and "diverged" mean the pin is not on main's history.
function githubCompareStatus(repository, commit) {
  return execFileSync(
    'gh',
    ['api', `repos/${repository}/compare/${commit}...main`, '--jq', '.status'],
    { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] },
  ).trim()
}

/**
 * Fails unless the pinned Vouchington commit is reachable from the producer's main branch, so
 * privileged jobs only ever execute reviewed, merged producer code. Returns the pin.
 */
export async function assertPinnedCommitOnMain({ config, compare = githubCompareStatus }) {
  const { commit, repository } = await readContractConfig(config)
  let status
  try {
    status = compare(repository, commit)
  } catch (error) {
    fail(`cannot compare ${commit} with ${repository}@main: ${error.message}`)
  }
  if (status !== 'identical' && status !== 'ahead')
    fail(
      `${commit} is not reachable from ${repository}@main (compare status "${status}"); ` +
        'merge the producer change first, then pin its main commit',
    )
  return commit
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const [flag, config, ...extra] = process.argv.slice(2)
  if (flag !== '--config' || !config || !isAbsolute(config) || extra.length !== 0)
    throw new Error('Usage: node scripts/assert-pinned-commit-on-main.mjs --config <absolute-path>')
  process.stdout.write(`${await assertPinnedCommitOnMain({ config })}\n`)
}
