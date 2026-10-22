import { execFileSync } from 'node:child_process'
import { readFileSync, realpathSync } from 'node:fs'
import { join, resolve } from 'node:path'
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

async function main() {
  const [command, packageRootArgument, artifactRootArgument] = process.argv.slice(2)
  if (!['pack', 'restore'].includes(command) || !packageRootArgument || !artifactRootArgument) {
    throw new Error('Usage: android-state-cli.mjs pack|restore <package-root> <artifact-root>')
  }
  const packageRoot = resolve(packageRootArgument)
  const artifactRoot = resolve(artifactRootArgument)
  const cacheRoot = process.env.ANDROID_STATE_BUILD_CACHE_DIR ||
    join(realpathSync(execFileSync('getconf', ['DARWIN_USER_TEMP_DIR'], { encoding: 'utf8' }).trim()), 'gradle-build-cache')
  const identity = cliIdentity(packageRoot)
  assertCandidate(packageRoot, identity)
  const result = command === 'pack'
    ? await packState({ packageRoot, cacheRoot, artifactRoot, identity })
    : await restoreState({ packageRoot, cacheRoot, artifactRoot, identity })
  process.stdout.write(`${command} Android exact state ${JSON.stringify(result)}\n`)
}

main().catch(error => {
  process.stderr.write(`android-state-cli.mjs: ${error.message}\n`)
  process.exitCode = 1
})
