import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import {
  chmod,
  mkdir,
  mkdtemp,
  readFile,
  readdir,
  rm,
  stat,
  symlink,
  writeFile,
} from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  materializeSdkVersion,
  sdkArchiveSha512,
  sdkArchiveUrl,
  sdkVersion,
} from '../dotnet-clients/tooling/materialize-sdk-version-metadata.mjs'

const content = 'official target bytes\n'
const archiveTime = 1_700_000_000
function python(source, ...args) {
  const result = spawnSync('python3', ['-I', '-c', source, ...args], { encoding: 'utf8' })
  assert.equal(result.status, 0, result.stderr)
}
async function fixture(t, { extraMember = '', otherBytes = content } = {}) {
  const root = await mkdtemp(join(tmpdir(), 'dotnet-sdk-materializer-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const dotnetRoot = join(root, 'mise-data/dotnet-root')
  const installed = join(dotnetRoot, 'sdk', sdkVersion)
  const file = join(installed, 'Microsoft.Common.targets')
  const other = join(dotnetRoot, 'dotnet')
  const archivePath = join(root, 'sdk.tar.gz')
  await mkdir(installed, { recursive: true })
  await writeFile(file, otherBytes)
  await writeFile(other, 'host untouched\n')
  python(
    `import io,tarfile,sys
archive,content,extra=sys.argv[1:]
with tarfile.open(archive,'w:gz') as tar:
 for name in ('.','./sdk','./sdk/10.0.401'):
  info=tarfile.TarInfo(name);info.type=tarfile.DIRTYPE;info.mode=0o755;info.mtime=1700000000;tar.addfile(info)
 data=content.encode();info=tarfile.TarInfo('./sdk/10.0.401/Microsoft.Common.targets');info.size=len(data);info.mode=0o644;info.mtime=1700000000;tar.addfile(info,io.BytesIO(data))
 if extra:
  data=b'extra';info=tarfile.TarInfo(extra);info.size=len(data);info.mode=0o644;info.mtime=1700000000;tar.addfile(info,io.BytesIO(data))`,
    archivePath,
    content,
    extraMember,
  )
  const archiveSha512 = createHash('sha512')
    .update(await readFile(archivePath))
    .digest('hex')
  return { root, dotnetRoot, installed, file, other, archivePath, archiveSha512 }
}

test('replaces only the SDK version tree with byte-identical official archive metadata', async t => {
  const state = await fixture(t)
  const before = (await stat(state.file, { bigint: true })).mtimeNs
  const result = materializeSdkVersion(state)
  assert.match(result, /official archive metadata preserved/)
  assert.equal(await readFile(state.file, 'utf8'), content)
  assert.equal(
    (await stat(state.file, { bigint: true })).mtimeNs,
    BigInt(archiveTime) * 1_000_000_000n,
  )
  assert.notEqual(before, (await stat(state.file, { bigint: true })).mtimeNs)
  assert.equal(await readFile(state.other, 'utf8'), 'host untouched\n')
  assert.deepEqual(await readdir(join(state.dotnetRoot, 'sdk')), [sdkVersion])
})

test('rejects archive checksum mismatch without changing installed SDK', async t => {
  const state = await fixture(t)
  const before = (await stat(state.file, { bigint: true })).mtimeNs
  assert.throws(
    () => materializeSdkVersion({ ...state, archiveSha512: sdkArchiveSha512 }),
    /SHA-512 mismatch/,
  )
  assert.equal((await stat(state.file, { bigint: true })).mtimeNs, before)
})

test('rejects changed bytes and extra installed members before replacement', async t => {
  const state = await fixture(t, { otherBytes: 'different content\n' })
  const before = (await stat(state.file, { bigint: true })).mtimeNs
  assert.throws(() => materializeSdkVersion(state), /files\/types\/bytes differ/)
  assert.equal((await stat(state.file, { bigint: true })).mtimeNs, before)
  await writeFile(state.file, content)
  await writeFile(join(state.installed, 'extra.dll'), 'extra')
  assert.throws(() => materializeSdkVersion(state), /files\/types\/bytes differ/)
  assert.equal(await readFile(join(state.installed, 'extra.dll'), 'utf8'), 'extra')
})

test('rejects installed permission drift before replacement', async t => {
  const state = await fixture(t)
  await chmod(state.file, 0o755)
  const before = (await stat(state.file, { bigint: true })).mtimeNs
  assert.throws(() => materializeSdkVersion(state), /files\/types\/bytes differ/)
  assert.equal((await stat(state.file, { bigint: true })).mtimeNs, before)
})

test('pins exact candidate SDK version, architecture and release digest', async () => {
  const root = new URL('../', import.meta.url)
  const global = JSON.parse(await readFile(new URL('global.json', root), 'utf8'))
  const mise = await readFile(new URL('.mise.toml', root), 'utf8')
  assert.equal(global.sdk.version, sdkVersion)
  assert.match(mise, /^dotnet = "10\.0\.401"$/mu)
  assert.equal(
    sdkArchiveUrl,
    'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-osx-arm64.tar.gz',
  )
  assert.match(sdkArchiveSha512, /^[0-9a-f]{128}$/u)
})

test('caps total archive members even outside the selected SDK version', async t => {
  const state = await fixture(t)
  python(
    `import tarfile,sys
with tarfile.open(sys.argv[1],'w:gz') as tar:
 for name in ('.','./sdk','./sdk/10.0.401'):
  info=tarfile.TarInfo(name);info.type=tarfile.DIRTYPE;tar.addfile(info)
 for index in range(10001):
  info=tarfile.TarInfo('./other-%05d'%index);info.type=tarfile.DIRTYPE;tar.addfile(info)`,
    state.archivePath,
  )
  state.archiveSha512 = createHash('sha512')
    .update(await readFile(state.archivePath))
    .digest('hex')
  assert.throws(() => materializeSdkVersion(state), /archive member cap exceeded/)
})

test('rejects archive traversal, links, and an installed symlink', async t => {
  const traversal = await fixture(t, { extraMember: './sdk/10.0.401/../escape' })
  assert.throws(() => materializeSdkVersion(traversal), /unsafe or duplicate archive member/)
  const linked = await fixture(t)
  await rm(linked.file)
  await symlink(linked.other, linked.file)
  assert.throws(() => materializeSdkVersion(linked), /link or special file/)
  const archiveLink = await fixture(t)
  python(
    `import tarfile,sys
with tarfile.open(sys.argv[1],'w:gz') as tar:
 for name in ('.','./sdk','./sdk/10.0.401'):
  info=tarfile.TarInfo(name);info.type=tarfile.DIRTYPE;tar.addfile(info)
 info=tarfile.TarInfo('./sdk/10.0.401/linked');info.type=tarfile.SYMTYPE;info.linkname='outside';tar.addfile(info)`,
    archiveLink.archivePath,
  )
  archiveLink.archiveSha512 = createHash('sha512')
    .update(await readFile(archiveLink.archivePath))
    .digest('hex')
  assert.throws(() => materializeSdkVersion(archiveLink), /unsafe or duplicate archive member/)
})
