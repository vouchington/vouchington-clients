import { createHash } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import {
  chmodSync, createReadStream, createWriteStream, existsSync, lstatSync, mkdirSync,
  readFileSync, renameSync, rmSync, statSync, utimesSync, writeFileSync,
} from 'node:fs'
import { basename, dirname, join, resolve, sep } from 'node:path'
import { Readable, Transform } from 'node:stream'
import { pipeline } from 'node:stream/promises'
import { fileURLToPath } from 'node:url'
import { createGunzip, createGzip } from 'node:zlib'
import { archiveChunks, ByteReader, magic, sha256File } from './android-state-codec.mjs'
import { assertCandidate, assertIdentity, assertMemberPath, defaultLimits, inventoryState, selectedRoots } from './android-state-inventory.mjs'

export { inventoryState } from './android-state-inventory.mjs'

const archiveName = 'android-state.gz'
const manifestName = 'android-state.json'

export async function packState({ packageRoot, cacheRoot, artifactRoot, identity, limits = defaultLimits }) {
  assertIdentity(identity)
  mkdirSync(artifactRoot, { recursive: true })
  const inventory = inventoryState(packageRoot, cacheRoot, limits)
  const archive = join(artifactRoot, archiveName)
  const started = Date.now()
  const deadline = started + limits.transferMs
  const digest = createHash('sha256')
  let compressedBytes = 0
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), limits.transferMs)
  try {
    await pipeline(
      Readable.from(archiveChunks(inventory.entries, identity, deadline)),
      createGzip({ level: 1 }),
      new Transform({
        transform(chunk, _encoding, callback) {
          compressedBytes += chunk.length
          if (compressedBytes > limits.compressedBytes || Date.now() > deadline) {
            callback(new Error('Android state exceeded compressed-byte or compression-time cap'))
          } else {
            digest.update(chunk)
            callback(null, chunk)
          }
        },
      }),
      createWriteStream(archive, { flags: 'wx', mode: 0o600 }),
      { signal: controller.signal },
    )
  } catch (error) {
    rmSync(archive, { force: true })
    throw error
  } finally {
    clearTimeout(timer)
  }
  const manifest = {
    format: 1,
    identity,
    sha256: digest.digest('hex'),
    entries: inventory.entries.length,
    unpackedBytes: inventory.unpackedBytes,
    compressedBytes,
  }
  writeFileSync(join(artifactRoot, manifestName), `${JSON.stringify(manifest)}\n`, { flag: 'wx', mode: 0o600 })
  return { ...manifest, scanMs: inventory.scanMs, packMs: Date.now() - started }
}

function checkedDestination(root, path) {
  assertMemberPath(path)
  const target = join(root, path)
  if (!target.startsWith(`${root}${sep}`)) throw new Error(`Android state path escaped root: ${path}`)
  return target
}

export async function restoreState({ packageRoot, cacheRoot, artifactRoot, identity, limits = defaultLimits }) {
  assertIdentity(identity)
  const manifestPath = join(artifactRoot, manifestName)
  if (statSync(manifestPath).size > 8192) throw new Error('Android state manifest exceeds 8 KiB')
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
  if (manifest.format !== 1 || JSON.stringify(manifest.identity) !== JSON.stringify(identity) ||
      !Number.isSafeInteger(manifest.entries) || manifest.entries > limits.entries ||
      !Number.isSafeInteger(manifest.unpackedBytes) || manifest.unpackedBytes > limits.unpackedBytes ||
      !Number.isSafeInteger(manifest.compressedBytes) || manifest.compressedBytes > limits.compressedBytes ||
      !/^[0-9a-f]{64}$/u.test(manifest.sha256)) {
    throw new Error('Android state manifest identity or limits mismatch')
  }
  const archive = join(artifactRoot, archiveName)
  if (statSync(archive).size !== manifest.compressedBytes) throw new Error('Android state archive size mismatch')
  const started = Date.now()
  const deadline = started + limits.transferMs
  const hash = await sha256File(archive, limits.compressedBytes, deadline)
  if (hash.digest !== manifest.sha256) throw new Error('Android state archive hash mismatch')
  const staging = join(artifactRoot, 'extracted')
  if (existsSync(staging)) throw new Error('Android state extraction target already exists')
  mkdirSync(staging, { mode: 0o700 })
  mkdirSync(join(staging, 'Android'), { mode: 0o700 })
  const reader = new ByteReader(createReadStream(archive).pipe(createGunzip()), deadline, limits.unpackedBytes + 2 * 8192 * limits.entries)
  const observed = { entries: 0, unpackedBytes: 0 }
  try {
    if (!(await reader.read(magic.length)).equals(magic)) throw new Error('Android state magic mismatch')
    const archiveIdentity = await reader.header()
    if (JSON.stringify(archiveIdentity) !== JSON.stringify(identity)) throw new Error('Android state header identity mismatch')
    let previous = ''
    let member
    while ((member = await reader.header()) !== null) {
      observed.entries += 1
      if (observed.entries > limits.entries || member.path <= previous) {
        throw new Error('Android state member count or order invalid')
      }
      previous = member.path
      const target = checkedDestination(staging, member.path)
      const parent = dirname(target)
      if (!lstatSync(parent).isDirectory()) throw new Error(`Android state parent is not a directory: ${member.path}`)
      if (!Number.isSafeInteger(member.mode) || member.mode < 0 || member.mode > 0o777) {
        throw new Error(`Android state mode is invalid: ${member.path}`)
      }
      if (member.type === 'directory' && member.size === 0) {
        mkdirSync(target, { mode: 0o700 })
      } else if (member.type === 'file' && Number.isSafeInteger(member.size) && member.size >= 0 &&
          Number.isFinite(member.mtimeMs) && member.mtimeMs >= 0) {
        observed.unpackedBytes += member.size
        if (observed.unpackedBytes > limits.unpackedBytes) throw new Error('Android state file-byte cap exceeded')
        const file = createWriteStream(target, { flags: 'wx', mode: 0o600 })
        try {
          let remaining = member.size
          for await (const chunk of (async function* () {
            while (remaining) {
              const data = await reader.read(Math.min(remaining, 64 * 1024))
              remaining -= data.length
              yield data
            }
          })()) {
            if (!file.write(chunk)) await new Promise(resolveDrain => file.once('drain', resolveDrain))
          }
          await new Promise((resolveFinish, reject) => file.end(error => error ? reject(error) : resolveFinish()))
          chmodSync(target, member.mode)
          utimesSync(target, member.mtimeMs / 1000, member.mtimeMs / 1000)
        } catch (error) {
          file.destroy()
          throw error
        }
      } else {
        throw new Error(`Android state member type invalid: ${member.path}`)
      }
    }
    await reader.end()
    if (observed.entries !== manifest.entries || observed.unpackedBytes !== manifest.unpackedBytes) {
      throw new Error('Android state unpacked inventory mismatch')
    }
    for (const { path, source } of selectedRoots(packageRoot, cacheRoot)) {
      const staged = checkedDestination(staging, path)
      if (!lstatSync(staged).isDirectory()) throw new Error(`Android state root missing: ${path}`)
      if (existsSync(source)) {
        if (lstatSync(source).isSymbolicLink() || !lstatSync(source).isDirectory()) {
          throw new Error(`Android state destination is unsafe: ${path}`)
        }
        rmSync(source, { recursive: true })
      }
      mkdirSync(dirname(source), { recursive: true })
      renameSync(staged, source)
    }
    return { ...manifest, restoreMs: Date.now() - started }
  } finally {
    rmSync(staging, { recursive: true, force: true })
  }
}

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
    throw new Error('Usage: android-state-handoff.mjs pack|restore <package-root> <artifact-root>')
  }
  const packageRoot = resolve(packageRootArgument)
  const artifactRoot = resolve(artifactRootArgument)
  const cacheRoot = process.env.ANDROID_STATE_BUILD_CACHE_DIR ||
    join(execFileSync('getconf', ['DARWIN_USER_TEMP_DIR'], { encoding: 'utf8' }).trim(), 'gradle-build-cache')
  const identity = cliIdentity(packageRoot)
  assertCandidate(packageRoot, identity)
  const result = command === 'pack'
    ? await packState({ packageRoot, cacheRoot, artifactRoot, identity })
    : await restoreState({ packageRoot, cacheRoot, artifactRoot, identity })
  process.stdout.write(`${command} Android exact state ${JSON.stringify(result)}\n`)
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch(error => {
    process.stderr.write(`${basename(import.meta.filename)}: ${error.message}\n`)
    process.exitCode = 1
  })
}
