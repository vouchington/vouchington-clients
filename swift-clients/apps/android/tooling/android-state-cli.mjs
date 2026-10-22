import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { pathToFileURL } from 'node:url'
import { packState, restoreState } from './android-state-handoff.mjs'
import { assertCandidate } from './android-state-inventory.mjs'

function cliIdentity(packageRoot) {
  const gradle = readFileSync(join(packageRoot, 'Android/gradle/wrapper/gradle-wrapper.properties'), 'utf8')
    .match(/gradle-(\d+\.\d+\.\d+)-bin\.zip/u)?.[1]
  return {
    revision: process.env.CANDIDATE_REVISION ?? '',
    run: process.env.GITHUB_RUN_ID ?? '',
    attempt: process.env.GITHUB_RUN_ATTEMPT ?? '',
    gradle: gradle ?? '',
  }
}

export function selectedBuildCacheRoot() {
  return execFileSync(
    'bash',
    [join(import.meta.dirname, 'gradle-build-cache-dir.sh')],
    { encoding: 'utf8' },
  ).trim()
}

async function main() {
  const [command, packageRootArgument, artifactRootArgument] = process.argv.slice(2)
  if (!['pack', 'restore'].includes(command) || !packageRootArgument || !artifactRootArgument) {
    throw new Error('Usage: android-state-cli.mjs pack|restore <package-root> <artifact-root>')
  }
  const packageRoot = resolve(packageRootArgument)
  const workspaceRoot = process.env.GITHUB_WORKSPACE
  if (!workspaceRoot) throw new Error('GITHUB_WORKSPACE is required for exact-run Android state')
  const checkoutRoot = join(resolve(workspaceRoot), 'candidate-clients')
  const artifactRoot = resolve(artifactRootArgument)
  const cacheRoot = selectedBuildCacheRoot()
  const identity = cliIdentity(packageRoot)
  assertCandidate(checkoutRoot, identity)
  const result = command === 'pack'
    ? await packState({ packageRoot, checkoutRoot, cacheRoot, artifactRoot, identity })
    : await restoreState({ packageRoot, checkoutRoot, cacheRoot, artifactRoot, identity })
  process.stdout.write(`${command} Android exact state ${JSON.stringify(result)}\n`)
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch(error => {
    process.stderr.write(`android-state-cli.mjs: ${error.message}\n`)
    process.exitCode = 1
  })
}
