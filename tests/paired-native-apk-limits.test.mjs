import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { createReadStream } from 'node:fs'
import { mkdtemp, open, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'
import { listRegularFiles, verifyApkBundle } from '../scripts/paired-native-test-evidence.mjs'
import { sha256 } from '../scripts/sha256.mjs'

it('accepts a debug APK beyond the generic artifact limit without widening other artifacts', async () => {
  const root = await mkdtemp(join(tmpdir(), 'paired-native-large-apk-'))
  try {
    const appPath = join(root, 'app-debug.apk')
    const app = await open(appPath, 'w')
    try {
      await app.truncate(256 * 1024 * 1024 + 1)
    } finally {
      await app.close()
    }
    const hash = createHash('sha256')
    for await (const chunk of createReadStream(appPath)) hash.update(chunk)
    const appHash = hash.digest('hex')
    const tests = Buffer.from('candidate test bytes')
    await writeFile(join(root, 'app-debug-androidTest.apk'), tests)
    await writeFile(join(root, 'candidate-revision.txt'), `${'a'.repeat(40)}\n`)
    await writeFile(
      join(root, 'sha256sums.txt'),
      `${appHash}  app-debug.apk\n${sha256(tests)}  app-debug-androidTest.apk\n`,
    )
    assert.deepEqual(await verifyApkBundle(root, 'a'.repeat(40)), {
      'app-debug.apk': appHash,
      'app-debug-androidTest.apk': sha256(tests),
    })
    await assert.rejects(listRegularFiles(root), /size limit/u)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

it('rejects oversized APKs and oversized APK metadata before reading their contents', async () => {
  for (const [name, size] of [
    ['app-debug.apk', 384 * 1024 * 1024 + 1],
    ['app-debug-androidTest.apk', 32 * 1024 * 1024 + 1],
    ['candidate-revision.txt', 129],
    ['sha256sums.txt', 1025],
  ]) {
    const root = await mkdtemp(join(tmpdir(), 'paired-native-oversize-'))
    try {
      const file = await open(join(root, name), 'w')
      try {
        await file.truncate(size)
      } finally {
        await file.close()
      }
      await assert.rejects(verifyApkBundle(root, 'a'.repeat(40)), /size limit/u)
    } finally {
      await rm(root, { recursive: true, force: true })
    }
  }
})
