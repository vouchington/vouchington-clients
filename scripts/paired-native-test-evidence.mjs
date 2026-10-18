import { createHash } from 'node:crypto'
import { lstat, opendir, readFile } from 'node:fs/promises'
import { join } from 'node:path'

const fail = message => {
  throw new Error(`Paired native test evidence rejected: ${message}`)
}

export function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex')
}

export function validateNativeAbiEntries(entries) {
  for (const name of ['libSkipAndroidBridge.so', 'libSwiftJNI.so', 'libVouchaAndroid.so'])
    if (!entries.includes(`lib/arm64-v8a/${name}`)) fail(`APK lacks ARM64 ${name}`)
  for (const entry of entries) {
    const match = /^lib\/([^/]+)\/[^/]+\.so$/u.exec(entry)
    if (match && match[1] !== 'arm64-v8a') fail(`APK advertises unsupported native ABI ${match[1]}`)
  }
  return true
}

function exactKeys(value, keys, label) {
  if (!value || typeof value !== 'object' || Array.isArray(value))
    fail(`${label} must be an object`)
  const actual = Object.keys(value).sort()
  const expected = [...keys].sort()
  if (JSON.stringify(actual) !== JSON.stringify(expected))
    fail(`${label} fields differ from the fixed schema`)
}

export function bindArtifact({ artifact, expected, runId, runAttempt }) {
  exactKeys(expected, ['id', 'digest', 'name'], 'expected artifact')
  if (!/^[0-9a-f]{64}$/u.test(expected.digest))
    fail('upload action digest must be a SHA-256 hex value')
  const apiDigest =
    typeof artifact?.digest === 'string' ? /^sha256:([0-9a-f]{64})$/u.exec(artifact.digest) : null
  if (!artifact || artifact.id !== expected.id || apiDigest?.[1] !== expected.digest)
    fail('artifact API identity differs from the upload action output')
  if (artifact.name !== expected.name || artifact.expired !== false)
    fail('artifact name or expiration state is invalid')
  if (String(artifact.workflow_run?.id) !== String(runId))
    fail('artifact belongs to another workflow run')
  if (Number(runAttempt) !== 1 || Number(artifact.workflow_run?.run_attempt ?? runAttempt) !== 1)
    fail('workflow run attempt is not the original attempt')
  return true
}

export async function listRegularFiles(root, { maxFiles = 16, maxDepth = 2, fileByteLimits } = {}) {
  const rootInfo = await lstat(root)
  if (!rootInfo.isDirectory() || rootInfo.isSymbolicLink())
    fail('artifact root must be a real directory')
  const files = new Map()
  let totalBytes = 0
  let entriesSeen = 0
  async function walk(directory, prefix = '', depth = 0) {
    if (depth > maxDepth) fail('artifact exceeds the fixed file-tree limit')
    const handle = await opendir(directory)
    for await (const entry of handle) {
      entriesSeen += 1
      if (entriesSeen > maxFiles) fail('artifact exceeds the fixed file-tree limit')
      const path = join(directory, entry.name)
      const name = prefix ? `${prefix}/${entry.name}` : entry.name
      const info = await lstat(path)
      if (info.isSymbolicLink()) fail(`artifact contains a symlink: ${name}`)
      if (info.isDirectory()) {
        if (depth >= maxDepth) fail('artifact exceeds the fixed file-tree limit')
        await walk(path, name, depth + 1)
      } else if (info.isFile()) {
        if (fileByteLimits && !fileByteLimits.has(name))
          fail('artifact file set differs from the fixed allowlist')
        const fileLimit = fileByteLimits?.get(name) ?? 256 * 1024 * 1024
        if (info.size > fileLimit || totalBytes + info.size > 512 * 1024 * 1024)
          fail('artifact exceeds the fixed size limit')
        totalBytes += info.size
        files.set(name, await readFile(path))
      } else fail(`artifact contains a special file: ${name}`)
    }
  }
  await walk(root)
  return files
}

export async function verifyApkBundle(root, expectedRevision) {
  const fileByteLimits = new Map([
    ['app-debug.apk', 384 * 1024 * 1024],
    ['app-debug-androidTest.apk', 32 * 1024 * 1024],
    ['candidate-revision.txt', 128],
    ['sha256sums.txt', 1024],
  ])
  const allowed = [...fileByteLimits.keys()]
  const files = await listRegularFiles(root, {
    maxFiles: allowed.length,
    maxDepth: 0,
    fileByteLimits,
  })
  if (
    files.size !== allowed.length ||
    JSON.stringify([...files.keys()].sort()) !== JSON.stringify([...allowed].sort())
  )
    fail('APK artifact file set differs from the fixed allowlist')
  if (files.get('candidate-revision.txt').toString('utf8').trim() !== expectedRevision)
    fail('APK artifact revision does not match the reviewed clients merge SHA')
  const sums = files.get('sha256sums.txt').toString('utf8').trim().split('\n')
  const expectedSums = ['app-debug.apk', 'app-debug-androidTest.apk']
    .map(name => `${sha256(files.get(name))}  ${name}`)
    .sort()
  if (JSON.stringify(sums.sort()) !== JSON.stringify(expectedSums))
    fail('APK checksums are invalid')
  return Object.fromEntries(
    ['app-debug.apk', 'app-debug-androidTest.apk'].map(name => [name, sha256(files.get(name))]),
  )
}

export function parseInstrumentationOutput(output, method) {
  const [className, methodName] = method.split('#')
  const normalized = output.replaceAll('\r', '')
  if (
    !normalized.includes(`INSTRUMENTATION_STATUS: class=voucha.android.${className}`) ||
    !normalized.includes(`INSTRUMENTATION_STATUS: test=${methodName}`)
  )
    fail(`instrumentation did not report the expected method ${method}`)
  if (
    !/^OK \(1 test\)$/mu.test(normalized) ||
    (normalized.match(/^INSTRUMENTATION_STATUS_CODE: 0$/gmu) ?? []).length !== 1
  )
    fail(`instrumentation did not report exactly one passing test for ${method}`)
  if (!/^INSTRUMENTATION_CODE: -1$/mu.test(normalized)) fail(`instrumentation failed for ${method}`)
  if (/INSTRUMENTATION_FAILED|FAILURES!!!|INSTRUMENTATION_RESULT: shortMsg/u.test(normalized))
    fail(`instrumentation reported a failure for ${method}`)
  return { method, exitCode: 0, tests: 1 }
}
