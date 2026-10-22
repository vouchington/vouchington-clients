import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import {
  chmod,
  mkdir,
  mkdtemp,
  readFile,
  readlink,
  rename,
  rm,
  stat,
  symlink,
  writeFile,
} from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, relative, resolve } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { gunzipSync, gzipSync } from 'node:zlib'
import {
  inventoryState,
  packState,
  restoreState,
} from '../swift-clients/apps/android/tooling/android-state-handoff.mjs'

const identity = { revision: 'a'.repeat(40), run: '123', attempt: '2', gradle: '9.4.1' }
const limits = {
  entries: 100,
  unpackedBytes: 1024,
  compressedBytes: 1024,
  scanMs: 5_000,
  transferMs: 5_000,
}

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), 'android-state-handoff-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const packageRoot = join(root, 'checkout/swift-clients/apps/android')
  const checkoutRoot = join(root, 'checkout')
  const cacheRoot = join(root, 'cache')
  const artifactRoot = join(root, 'artifact')
  await mkdir(join(packageRoot, '.build/plugins/outputs'), { recursive: true })
  await mkdir(join(packageRoot, 'Android/.gradle'), { recursive: true })
  await mkdir(cacheRoot)
  await writeFile(join(packageRoot, '.build/plugins/outputs/native.o'), 'native')
  await chmod(join(packageRoot, '.build/plugins/outputs/native.o'), 0o755)
  await writeFile(join(packageRoot, 'Android/.gradle/task-state'), 'task-state')
  await writeFile(join(cacheRoot, 'cache-entry'), 'cache')
  return { root, packageRoot, checkoutRoot, cacheRoot, artifactRoot }
}

test('packs and restores exact candidate state with bounded contents', async t => {
  const state = await fixture(t)
  const nativePath = join(state.packageRoot, '.build/plugins/outputs/native.o')
  const directoryPath = join(state.packageRoot, '.build/plugins/outputs')
  const preciseTime = '1792634209267950316'
  const timestamp = spawnSync(
    'python3',
    [
      '-I',
      '-c',
      'import os,sys; t=int(sys.argv[2]); os.utime(sys.argv[1], ns=(t,t))',
      nativePath,
      preciseTime,
    ],
    { encoding: 'utf8' },
  )
  assert.equal(timestamp.status, 0, timestamp.stderr)
  const nativeTime = (await stat(nativePath, { bigint: true })).mtimeNs
  assert.equal(nativeTime.toString(), preciseTime)
  const directoryMode = (await stat(directoryPath)).mode & 0o777
  const packed = await packState({ ...state, identity, limits })
  assert.equal(packed.format, 2)
  assert.equal(packed.unpackedBytes, 21)
  assert.ok(packed.compressedBytes > 0 && packed.compressedBytes <= limits.compressedBytes)
  await rm(join(state.packageRoot, '.build'), { recursive: true })
  await rm(join(state.packageRoot, 'Android/.gradle'), { recursive: true })
  await rm(state.cacheRoot, { recursive: true })
  const restored = await restoreState({ ...state, identity, limits })
  assert.equal(restored.sha256, packed.sha256)
  assert.equal(
    await readFile(join(state.packageRoot, '.build/plugins/outputs/native.o'), 'utf8'),
    'native',
  )
  assert.equal((await stat(nativePath)).mode & 0o777, 0o755)
  assert.equal((await stat(nativePath, { bigint: true })).mtimeNs, nativeTime)
  assert.equal((await stat(directoryPath)).mode & 0o777, directoryMode)
  assert.equal(
    await readFile(join(state.packageRoot, 'Android/.gradle/task-state'), 'utf8'),
    'task-state',
  )
  assert.equal(await readFile(join(state.cacheRoot, 'cache-entry'), 'utf8'), 'cache')
})

test('restores only the internal SwiftPM debug directory alias', async t => {
  const state = await fixture(t)
  await mkdir(join(state.packageRoot, '.build/arm64-apple-macosx/debug'), { recursive: true })
  await symlink('arm64-apple-macosx/debug', join(state.packageRoot, '.build/debug'))
  await packState({ ...state, identity, limits })
  await rm(join(state.packageRoot, '.build'), { recursive: true })
  await restoreState({ ...state, identity, limits })
  assert.equal(await readlink(join(state.packageRoot, '.build/debug')), 'arm64-apple-macosx/debug')
})

test('restores a generated Skip native SwiftPM scratch debug alias', async t => {
  const state = await fixture(t)
  const scratch = join(
    state.packageRoot,
    '.build/plugins/outputs/android/VouchaAndroid/destination/skipstone/VouchaAndroid/build/swift',
  )
  await mkdir(join(scratch, 'aarch64-unknown-linux-android/debug'), { recursive: true })
  await symlink('aarch64-unknown-linux-android/debug', join(scratch, 'debug'))
  await packState({ ...state, identity, limits })
  await rm(join(state.packageRoot, '.build'), { recursive: true })
  await restoreState({ ...state, identity, limits })
  assert.equal(await readlink(join(scratch, 'debug')), 'aarch64-unknown-linux-android/debug')
})

test('rebases Skipstone absolute source and package links onto the exact consumer checkout', async t => {
  const producer = await fixture(t)
  const consumer = await fixture(t)
  const swiftFolder =
    '.build/plugins/outputs/skip-keychain/SkipKeychain/destination/skipstone/SkipKeychain/src/main/swift'
  const producerLinks = join(producer.packageRoot, swiftFolder)
  await mkdir(join(producerLinks, 'Packages'), { recursive: true })
  await mkdir(join(producer.packageRoot, '.build/checkouts/skip-keychain/Sources'), {
    recursive: true,
  })
  await mkdir(join(producer.checkoutRoot, 'swift-clients/core/Sources'), { recursive: true })
  await symlink(
    join(producer.packageRoot, '.build/checkouts/skip-keychain/Sources'),
    join(producerLinks, 'Sources'),
  )
  await symlink(
    join(producer.checkoutRoot, 'swift-clients/core'),
    join(producerLinks, 'Packages/core'),
  )
  await packState({ ...producer, identity, limits: { ...limits, entries: 150 } })
  await mkdir(join(consumer.checkoutRoot, 'swift-clients/core/Sources'), { recursive: true })
  await rm(join(consumer.packageRoot, '.build'), { recursive: true })
  await rm(join(consumer.packageRoot, 'Android/.gradle'), { recursive: true })
  await rm(consumer.cacheRoot, { recursive: true })
  await restoreState({
    ...consumer,
    artifactRoot: producer.artifactRoot,
    identity,
    limits: { ...limits, entries: 150 },
  })
  const links = join(consumer.packageRoot, swiftFolder)
  assert.equal(
    resolve(links, await readlink(join(links, 'Sources'))),
    join(consumer.packageRoot, '.build/checkouts/skip-keychain/Sources'),
  )
  assert.equal(
    resolve(join(links, 'Packages'), await readlink(join(links, 'Packages/core'))),
    join(consumer.checkoutRoot, 'swift-clients/core'),
  )
  assert.equal((await stat(join(links, 'Sources'))).isDirectory(), true)
  assert.equal((await stat(join(links, 'Packages/core'))).isDirectory(), true)
})

test('preserves Skipstone module, Kotlin, resource, buildSrc and AndroidTest links', async t => {
  const state = await fixture(t)
  const generated = '.build/plugins/outputs/skip-keychain/SkipKeychain/destination/skipstone'
  const generatedRoot = join(state.packageRoot, generated)
  const moduleRoot = join(generatedRoot, 'SkipKeychain')
  const otherModule = join(
    state.packageRoot,
    '.build/plugins/outputs/skip-lib/SkipLib/destination/skipstone/SkipLib',
  )
  const source = join(state.checkoutRoot, 'swift-clients/core')
  await mkdir(join(moduleRoot, 'src/main/kotlin'), { recursive: true })
  await mkdir(join(moduleRoot, 'src/main/assets/en.lproj'), { recursive: true })
  await mkdir(join(moduleRoot, 'src/main/res'), { recursive: true })
  await mkdir(join(moduleRoot, 'src/test'), { recursive: true })
  await mkdir(otherModule, { recursive: true })
  await mkdir(join(otherModule, 'src/main/kotlin'), { recursive: true })
  await mkdir(join(source, 'Skip/buildSrc'), { recursive: true })
  await mkdir(join(source, 'Resources'), { recursive: true })
  await writeFile(join(source, 'Skip/Api.kt'), 'api')
  await writeFile(join(source, 'Resources/logo.dat'), 'logo')
  await writeFile(join(otherModule, 'src/main/kotlin/Lib.kt'), 'lib')
  await symlink('logo.dat', join(source, 'Resources/logo-alias.dat'))
  await symlink(join(source, 'Resources/logo.dat'), join(source, 'Resources/logo-absolute.dat'))
  await symlink(relative(generatedRoot, otherModule), join(generatedRoot, 'SkipLib'))
  await symlink(join(source, 'Skip/buildSrc'), join(generatedRoot, 'buildSrc'))
  await symlink(join(source, 'Skip'), join(moduleRoot, 'ext'))
  await symlink(join(source, 'Skip/Api.kt'), join(moduleRoot, 'src/main/kotlin/Api.kt'))
  await symlink(join(source, 'Resources/logo.dat'), join(moduleRoot, 'src/main/assets/logo.dat'))
  await symlink(join(source, 'Resources/logo.dat'), join(moduleRoot, 'src/main/res/logo.dat'))
  await symlink(
    join(source, 'Resources/logo-alias.dat'),
    join(moduleRoot, 'src/main/assets/logo-alias.dat'),
  )
  await symlink(
    join(source, 'Resources/logo-absolute.dat'),
    join(moduleRoot, 'src/main/assets/logo-absolute.dat'),
  )
  await symlink(
    relative(
      join(moduleRoot, 'src/main/kotlin'),
      join(generatedRoot, 'SkipLib/src/main/kotlin/Lib.kt'),
    ),
    join(moduleRoot, 'src/main/kotlin/FromLib.kt'),
  )
  await symlink('en.lproj', join(moduleRoot, 'src/main/assets/base.lproj'))
  await symlink('test', join(moduleRoot, 'src/androidTest'))
  const biggerLimits = { ...limits, entries: 200, unpackedBytes: 4096, compressedBytes: 4096 }
  await packState({ ...state, identity, limits: biggerLimits })
  await rm(join(state.packageRoot, '.build'), { recursive: true })
  await restoreState({ ...state, identity, limits: biggerLimits })
  assert.equal((await stat(join(generatedRoot, 'SkipLib'))).isDirectory(), true)
  assert.equal((await stat(join(generatedRoot, 'buildSrc'))).isDirectory(), true)
  assert.equal((await stat(join(moduleRoot, 'ext'))).isDirectory(), true)
  assert.equal(await readFile(join(moduleRoot, 'src/main/kotlin/Api.kt'), 'utf8'), 'api')
  assert.equal(await readFile(join(moduleRoot, 'src/main/assets/logo.dat'), 'utf8'), 'logo')
  assert.equal(await readFile(join(moduleRoot, 'src/main/res/logo.dat'), 'utf8'), 'logo')
  assert.equal(await readFile(join(moduleRoot, 'src/main/assets/logo-alias.dat'), 'utf8'), 'logo')
  assert.equal(
    await readFile(join(moduleRoot, 'src/main/assets/logo-absolute.dat'), 'utf8'),
    'logo',
  )
  assert.equal(await readFile(join(moduleRoot, 'src/main/kotlin/FromLib.kt'), 'utf8'), 'lib')
  assert.equal((await stat(join(moduleRoot, 'src/main/assets/base.lproj'))).isDirectory(), true)
  assert.equal((await stat(join(moduleRoot, 'src/androidTest'))).isDirectory(), true)
})

test('rejects cycles and over-deep Skipstone link graphs', async t => {
  const cycle = await fixture(t)
  const moduleRoot = join(
    cycle.packageRoot,
    '.build/plugins/outputs/skip-keychain/SkipKeychain/destination/skipstone/SkipKeychain',
  )
  await mkdir(moduleRoot, { recursive: true })
  await symlink('B', join(moduleRoot, 'A'))
  await symlink('A', join(moduleRoot, 'B'))
  assert.throws(
    () => inventoryState(cycle.packageRoot, cycle.cacheRoot, cycle.checkoutRoot, limits),
    /cycle or exceeds depth cap/u,
  )

  const deep = await fixture(t)
  const deepRoot = join(
    deep.packageRoot,
    '.build/plugins/outputs/skip-keychain/SkipKeychain/destination/skipstone/SkipKeychain',
  )
  await mkdir(deepRoot, { recursive: true })
  await writeFile(join(deepRoot, 'terminal'), 'done')
  for (let index = 0; index < 34; index++) {
    await symlink(index === 33 ? 'terminal' : `link-${index + 1}`, join(deepRoot, `link-${index}`))
  }
  assert.throws(
    () =>
      inventoryState(deep.packageRoot, deep.cacheRoot, deep.checkoutRoot, {
        ...limits,
        entries: 200,
      }),
    /cycle or exceeds depth cap/u,
  )
})

test('rejects a Skipstone target whose candidate-source link escapes', async t => {
  const state = await fixture(t)
  const generatedRoot = join(
    state.packageRoot,
    '.build/plugins/outputs/skip-keychain/SkipKeychain/destination/skipstone/SkipKeychain',
  )
  const source = join(state.checkoutRoot, 'swift-clients/core/Resources')
  const outside = join(state.root, 'outside')
  await mkdir(generatedRoot, { recursive: true })
  await mkdir(source, { recursive: true })
  await mkdir(outside)
  await symlink(relative(source, outside), join(source, 'bad-link'))
  await symlink(join(source, 'bad-link'), join(generatedRoot, 'bad-resource'))
  assert.throws(
    () => inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits),
    /link graph escaped selected candidate state/u,
  )
  await rm(join(source, 'bad-link'))
  await symlink('/etc', join(source, 'bad-link'))
  assert.throws(
    () => inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits),
    /link graph escaped selected candidate state/u,
  )
})

test('rejects a Skipstone mirror link outside the candidate checkout', async t => {
  const state = await fixture(t)
  const links = join(
    state.packageRoot,
    '.build/plugins/outputs/skip-keychain/SkipKeychain/destination/skipstone/SkipKeychain/src/main/swift',
  )
  await mkdir(links, { recursive: true })
  await symlink('/etc', join(links, 'Sources'))
  assert.throws(
    () => inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits),
    /mirror target escaped candidate checkout/u,
  )
})

test('rejects an external Skipstone module link', async t => {
  const state = await fixture(t)
  const generatedRoot = join(
    state.packageRoot,
    '.build/plugins/outputs/skip-keychain/SkipKeychain/destination/skipstone',
  )
  await mkdir(generatedRoot, { recursive: true })
  await symlink('/etc', join(generatedRoot, 'SkipLib'))
  assert.throws(
    () => inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits),
    /mirror target escaped candidate checkout/u,
  )
})

test('rejects a mirrored target that traverses a symlink before dot-dot', async t => {
  const state = await fixture(t)
  const links = join(
    state.packageRoot,
    '.build/plugins/outputs/skip-keychain/SkipKeychain/destination/skipstone/SkipKeychain/src/main/swift',
  )
  const source = join(state.checkoutRoot, 'swift-clients/core')
  const outside = join(state.root, 'outside/deep')
  await mkdir(links, { recursive: true })
  await mkdir(source, { recursive: true })
  await mkdir(outside, { recursive: true })
  await mkdir(join(source, 'bar'))
  await symlink(outside, join(source, 'jump'))
  await symlink(`${relative(links, source)}/jump/../bar`, join(links, 'Sources'))
  assert.throws(
    () => inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits),
    /Unsafe Android state symlink/u,
  )
})

test('rejects an escaping SwiftPM debug alias', async t => {
  const state = await fixture(t)
  await symlink('../../outside/debug', join(state.packageRoot, '.build/debug'))
  assert.throws(
    () => inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits),
    /Unsafe Android state symlink/u,
  )
})

test('rejects mismatched attempt, changed archive, external links and over-budget members', async t => {
  const state = await fixture(t)
  assert.throws(
    () =>
      inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, {
        ...limits,
        entries: 2,
      }),
    /member cap/u,
  )
  const linked = join(state.packageRoot, '.build/plugins/outputs/external')
  await symlink('/etc/passwd', linked)
  assert.throws(
    () => inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits),
    /symlink/u,
  )
  await rm(linked)
  await packState({ ...state, identity, limits })
  await assert.rejects(
    restoreState({ ...state, identity: { ...identity, attempt: '3' }, limits }),
    /identity or limits mismatch/u,
  )
  await writeFile(join(state.artifactRoot, 'android-state.gz'), 'forged')
  await assert.rejects(restoreState({ ...state, identity, limits }), /archive size mismatch/u)
})

for (const invalidTime of ['9999999999999999999', '0123456789012345678']) {
  test(`rejects forged nanosecond timestamp ${invalidTime} before replacing state`, async t => {
    const state = await fixture(t)
    await packState({ ...state, identity, limits })
    const archivePath = join(state.artifactRoot, 'android-state.gz')
    const manifestPath = join(state.artifactRoot, 'android-state.json')
    const bytes = gunzipSync(await readFile(archivePath))
    let offset = 5
    let replaced = false
    const chunks = [bytes.subarray(0, offset)]
    while (offset < bytes.length) {
      const length = bytes.readUInt32BE(offset)
      if (length === 0) {
        chunks.push(bytes.subarray(offset))
        break
      }
      const member = JSON.parse(bytes.subarray(offset + 4, offset + 4 + length).toString())
      const next = offset + 4 + length + (member.type === 'file' ? member.size : 0)
      if (!replaced && member.type === 'file') {
        member.mtimeNs = invalidTime
        const header = Buffer.from(JSON.stringify(member))
        const headerLength = Buffer.alloc(4)
        headerLength.writeUInt32BE(header.length)
        chunks.push(headerLength, header, bytes.subarray(offset + 4 + length, next))
        replaced = true
      } else {
        chunks.push(bytes.subarray(offset, next))
      }
      offset = next
    }
    assert.equal(replaced, true)
    const forged = gzipSync(Buffer.concat(chunks))
    await writeFile(archivePath, forged)
    const manifest = JSON.parse(await readFile(manifestPath, 'utf8'))
    manifest.compressedBytes = forged.length
    manifest.sha256 = createHash('sha256').update(forged).digest('hex')
    await writeFile(manifestPath, `${JSON.stringify(manifest)}\n`)
    await assert.rejects(restoreState({ ...state, identity, limits }), /member type invalid/u)
    assert.equal(
      await readFile(join(state.packageRoot, '.build/plugins/outputs/native.o'), 'utf8'),
      'native',
    )
  })
}

test('exact timestamp helper rejects a symlinked parent', async t => {
  const state = await fixture(t)
  const outside = join(state.root, 'outside')
  const file = join(outside, 'target')
  await mkdir(outside)
  await writeFile(file, 'outside')
  await symlink(outside, join(state.root, 'alias'))
  const before = (await stat(file, { bigint: true })).mtimeNs
  const helper = fileURLToPath(
    new URL('../swift-clients/apps/android/tooling/android-state-ns.py', import.meta.url),
  )
  const result = spawnSync('python3', ['-I', helper], {
    cwd: state.root,
    input: `${JSON.stringify({ path: 'alias/target', mtimeNs: '1792634209267950316' })}\n`,
    encoding: 'utf8',
  })
  assert.notEqual(result.status, 0)
  assert.match(result.stderr, /not a real directory/u)
  assert.equal((await stat(file, { bigint: true })).mtimeNs, before)
})

test('rejects a symlinked destination parent before replacing any generated state', async t => {
  const state = await fixture(t)
  await packState({ ...state, identity, limits })
  const outside = join(state.packageRoot, '..', 'outside')
  await mkdir(join(outside, '.gradle'), { recursive: true })
  await writeFile(join(outside, '.gradle/sentinel'), 'outside')
  await rm(join(state.packageRoot, 'Android'), { recursive: true })
  await symlink(outside, join(state.packageRoot, 'Android'))
  await assert.rejects(
    restoreState({ ...state, identity, limits }),
    /destination parent is unsafe/u,
  )
  assert.equal(await readFile(join(outside, '.gradle/sentinel'), 'utf8'), 'outside')
  assert.equal(
    await readFile(join(state.packageRoot, '.build/plugins/outputs/native.o'), 'utf8'),
    'native',
  )
})

test('rejects a symlinked package ancestor outside the checkout', async t => {
  const state = await fixture(t)
  await packState({ ...state, identity, limits })
  const external = join(state.root, 'outside')
  await mkdir(external)
  const swiftClients = join(state.root, 'checkout/swift-clients')
  await rename(swiftClients, join(external, 'swift-clients'))
  await symlink(join(external, 'swift-clients'), swiftClients)
  await assert.rejects(restoreState({ ...state, identity, limits }), /package ancestor is unsafe/u)
  assert.equal(
    await readFile(
      join(external, 'swift-clients/apps/android/.build/plugins/outputs/native.o'),
      'utf8',
    ),
    'native',
  )
})

test('rejects a relocated package even when its layout looks valid', async t => {
  const state = await fixture(t)
  await packState({ ...state, identity, limits })
  const relocated = join(state.root, 'relocated/swift-clients/apps/android')
  await mkdir(relocated, { recursive: true })
  await assert.rejects(
    restoreState({ ...state, packageRoot: relocated, identity, limits }),
    /Unexpected Android package layout/u,
  )
})
