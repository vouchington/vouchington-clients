import { appendFileSync, lstatSync, opendirSync } from 'node:fs'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'

export const cacheSaveLimits = {
  bytes: 512 * 1024 ** 2,
  entries: 100_000,
  scanMs: 30_000,
}

export function inspectGradleBuildCache(root, limits = cacheSaveLimits) {
  const rootStat = lstatSync(root)
  if (!rootStat.isDirectory() || rootStat.isSymbolicLink()) {
    throw new Error('Gradle task-output cache root must be a real directory')
  }

  const started = Date.now()
  const pending = [root]
  let entries = 0
  let bytes = 0
  while (pending.length > 0) {
    const directory = opendirSync(pending.pop())
    try {
      for (let member = directory.readSync(); member !== null; member = directory.readSync()) {
        if (Date.now() - started > limits.scanMs) {
          return { eligible: false, reason: 'scan-time', entries, bytes }
        }
        entries += 1
        if (entries > limits.entries) {
          return { eligible: false, reason: 'member-count', entries, bytes }
        }
        const memberPath = join(directory.path, member.name)
        const stat = lstatSync(memberPath)
        if (stat.isSymbolicLink() || (!stat.isDirectory() && !stat.isFile())) {
          throw new Error('Gradle task-output cache contains an unsafe member')
        }
        if (stat.isDirectory()) {
          pending.push(memberPath)
        } else {
          bytes += stat.size
          if (bytes > limits.bytes) {
            return { eligible: false, reason: 'logical-byte-size', entries, bytes }
          }
        }
      }
    } finally {
      directory.closeSync()
    }
  }
  return { eligible: true, reason: 'within-bounds', entries, bytes }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [, , root, output] = process.argv
  if (!root || !output) throw new Error('Pass the selected Gradle cache path and GITHUB_OUTPUT')
  const result = inspectGradleBuildCache(root)
  appendFileSync(output, `save=${result.eligible}\n`)
  console.log(`Gradle task-output cache: ${result.entries} entries, ${result.bytes} logical bytes; ${result.reason}`)
  if (!result.eligible) {
    console.log(`::warning::Gradle task-output cache save skipped: ${result.reason}`)
  }
}
