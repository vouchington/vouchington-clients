import { appendFile } from 'node:fs/promises'
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const [baseSha, headSha, outputPath] = process.argv.slice(2)
const required = {
  BASE_SHA: baseSha,
  EXPECTED_HEAD_SHA: headSha,
  GITHUB_OUTPUT: outputPath,
  PACKAGE_ECOSYSTEM: process.env.PACKAGE_ECOSYSTEM,
  DIRECTORY: process.env.DIRECTORY,
  UPDATED_DEPENDENCIES_JSON: process.env.UPDATED_DEPENDENCIES_JSON,
}
for (const [name, value] of Object.entries(required)) {
  if (typeof value !== 'string' || value.length === 0) throw new Error(`${name} must be nonempty`)
}

const changedPaths = execFileSync('git', ['diff', '--name-only', '-z', `${baseSha}...${headSha}`], {
  encoding: 'utf8',
})
  .split('\0')
  .filter(Boolean)

let repairKind = 'none'
if (
  changedPaths.some(path => path.startsWith('swift-clients/apps/android/')) &&
  required.PACKAGE_ECOSYSTEM === 'swift' &&
  required.DIRECTORY === '/swift-clients/apps/android'
) {
  const updatedDependencies = JSON.parse(required.UPDATED_DEPENDENCIES_JSON)
  if (!Array.isArray(updatedDependencies))
    throw new Error('updated dependencies metadata must be an array')
  if (
    updatedDependencies.some(dependency => dependency?.dependencyName === 'source.skip.tools/skip')
  ) {
    execFileSync(
      process.execPath,
      [
        fileURLToPath(new URL('./validate-dependabot-swift-repair.mjs', import.meta.url)),
        'pull-request',
        process.env.LIVE_PR_PATH,
        process.env.EXACT_DIFF_PATH,
        process.env.DEFAULT_BRANCH,
        process.env.GITHUB_REPOSITORY,
        process.env.HEAD_REF,
        baseSha,
        headSha,
      ],
      { stdio: 'inherit' },
    )
    repairKind = 'swift-android'
  }
}

await appendFile(outputPath, `repair-kind=${repairKind}\n`)
