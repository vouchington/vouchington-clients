import { createHash } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { pathToFileURL } from 'node:url'
import { selectedBuildCacheRoot } from './android-state-cli.mjs'
import { mergeKotlinCache, packKotlinCache } from './android-kotlin-cache.mjs'
import { fileDigest } from './android-kotlin-cache-files.mjs'

export function configurationDigest(checkoutRoot) {
  const prefix = 'swift-clients/apps/android/'
  const files = ['.mise.toml', ...[
    'Package.resolved', 'Android/settings.gradle.kts', 'Android/app/build.gradle.kts',
    'Android/gradle.properties', 'Android/gradle/wrapper/gradle-wrapper.properties',
    'tooling/materialize-skip-sdk.sh',
  ].map(path => prefix + path)]
  const digest = createHash('sha256')
  const deadline = Date.now() + 30_000
  for (const path of files) digest.update(path + '\0' + fileDigest(join(checkoutRoot, path), 1024 * 1024, deadline).sha256 + '\n')
  return digest.digest('hex')
}

function main() {
  const [command, packageArgument, artifactArgument, ...extra] = process.argv.slice(2)
  if (!['pack', 'merge'].includes(command) || !packageArgument || !artifactArgument || extra.length) throw Error('Usage: android-kotlin-cache-cli.mjs pack|merge <package-root> <artifact-root>')
  if (!process.env.GITHUB_WORKSPACE) throw Error('GITHUB_WORKSPACE is required')
  const packageRoot = resolve(packageArgument)
  const checkoutRoot = join(resolve(process.env.GITHUB_WORKSPACE), 'candidate-clients')
  const gradle = readFileSync(join(packageRoot, 'Android/gradle/wrapper/gradle-wrapper.properties'), 'utf8')
    .match(/gradle-(\d+\.\d+\.\d+)-bin\.zip/u)?.[1] ?? ''
  const identity = { revision: process.env.CANDIDATE_REVISION ?? '', run: process.env.GITHUB_RUN_ID ?? '', attempt: process.env.GITHUB_RUN_ATTEMPT ?? '', gradle }
  const options = { packageRoot, checkoutRoot, cacheRoot: selectedBuildCacheRoot(), artifactRoot: resolve(artifactArgument), identity, configurationSha256: configurationDigest(checkoutRoot) }
  const result = command === 'pack' ? packKotlinCache(options) : mergeKotlinCache(options)
  console.log(`${command} Android Kotlin cache ${JSON.stringify(result)}`)
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try { main() } catch (error) { console.error(`android-kotlin-cache-cli.mjs: ${error.message}`); process.exitCode = 1 }
}
