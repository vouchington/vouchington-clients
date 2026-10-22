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
  refPackVersion,
  sdkArchiveSha512,
  sdkArchiveUrl,
  sdkVersion,
} from '../dotnet-clients/tooling/materialize-sdk-version-metadata.mjs'

const content = 'official target bytes\n'
const referenceContent = 'official reference bytes\n'
const archiveTime = 1_700_000_000
function python(source, ...args) {
  const result = spawnSync('python3', ['-I', '-c', source, ...args], { encoding: 'utf8' })
  assert.equal(result.status, 0, result.stderr)
}
async function fixture(
  t,
  { extraMember = '', otherBytes = content, omitArchivePack = false } = {},
) {
  const root = await mkdtemp(join(tmpdir(), 'dotnet-sdk-materializer-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const dotnetRoot = join(root, 'mise-data/dotnet-root')
  const installed = join(dotnetRoot, 'sdk', sdkVersion)
  const file = join(installed, 'Microsoft.Common.targets')
  const referencePack = join(dotnetRoot, 'packs', 'Microsoft.NETCore.App.Ref', refPackVersion)
  const referenceFile = join(referencePack, 'ref', 'net10.0', 'Microsoft.CSharp.dll')
  const otherPackFile = join(dotnetRoot, 'packs', 'Unrelated.Workload.Ref', '1.0', 'leave.txt')
  const other = join(dotnetRoot, 'dotnet')
  const archivePath = join(root, 'sdk.tar.gz')
  await mkdir(installed, { recursive: true })
  await mkdir(join(referencePack, 'ref', 'net10.0'), { recursive: true })
  await mkdir(join(dotnetRoot, 'packs', 'Unrelated.Workload.Ref', '1.0'), { recursive: true })
  await writeFile(file, otherBytes)
  await writeFile(referenceFile, referenceContent)
  await writeFile(otherPackFile, 'unrelated pack untouched\n')
  await writeFile(other, 'host untouched\n')
  python(
    `import io,tarfile,sys
archive,content,reference,extra,omit_pack=sys.argv[1:]
with tarfile.open(archive,'w:gz') as tar:
 for name in ('.','./sdk','./sdk/10.0.401'):
  info=tarfile.TarInfo(name);info.type=tarfile.DIRTYPE;info.mode=0o755;info.mtime=1700000000;tar.addfile(info)
 data=content.encode();info=tarfile.TarInfo('./sdk/10.0.401/Microsoft.Common.targets');info.size=len(data);info.mode=0o644;info.mtime=1700000000;tar.addfile(info,io.BytesIO(data))
 if omit_pack!='yes':
  for name in ('./packs','./packs/Microsoft.NETCore.App.Ref','./packs/Microsoft.NETCore.App.Ref/10.0.12','./packs/Microsoft.NETCore.App.Ref/10.0.12/ref','./packs/Microsoft.NETCore.App.Ref/10.0.12/ref/net10.0'):
   info=tarfile.TarInfo(name);info.type=tarfile.DIRTYPE;info.mode=0o755;info.mtime=1700000000;tar.addfile(info)
  data=reference.encode();info=tarfile.TarInfo('./packs/Microsoft.NETCore.App.Ref/10.0.12/ref/net10.0/Microsoft.CSharp.dll');info.size=len(data);info.mode=0o644;info.mtime=1700000000;tar.addfile(info,io.BytesIO(data))
 if extra:
  data=b'extra';info=tarfile.TarInfo(extra);info.size=len(data);info.mode=0o644;info.mtime=1700000000;tar.addfile(info,io.BytesIO(data))`,
    archivePath,
    content,
    referenceContent,
    extraMember,
    omitArchivePack ? 'yes' : 'no',
  )
  const archiveSha512 = createHash('sha512')
    .update(await readFile(archivePath))
    .digest('hex')
  return {
    root,
    dotnetRoot,
    installed,
    file,
    referencePack,
    referenceFile,
    otherPackFile,
    other,
    archivePath,
    archiveSha512,
  }
}

test('replaces byte-identical SDK and reference-pack trees with official archive metadata', async t => {
  const state = await fixture(t)
  const before = (await stat(state.file, { bigint: true })).mtimeNs
  const referenceBefore = (await stat(state.referenceFile, { bigint: true })).mtimeNs
  const otherPackBefore = (await stat(state.otherPackFile, { bigint: true })).mtimeNs
  const result = materializeSdkVersion(state)
  assert.match(result, /official archive metadata preserved/)
  assert.equal(await readFile(state.file, 'utf8'), content)
  assert.equal(await readFile(state.referenceFile, 'utf8'), referenceContent)
  assert.equal(
    (await stat(state.file, { bigint: true })).mtimeNs,
    BigInt(archiveTime) * 1_000_000_000n,
  )
  assert.notEqual(before, (await stat(state.file, { bigint: true })).mtimeNs)
  assert.equal(
    (await stat(state.referenceFile, { bigint: true })).mtimeNs,
    BigInt(archiveTime) * 1_000_000_000n,
  )
  assert.notEqual(referenceBefore, (await stat(state.referenceFile, { bigint: true })).mtimeNs)
  assert.equal(await readFile(state.other, 'utf8'), 'host untouched\n')
  assert.equal(await readFile(state.otherPackFile, 'utf8'), 'unrelated pack untouched\n')
  assert.equal((await stat(state.otherPackFile, { bigint: true })).mtimeNs, otherPackBefore)
  assert.deepEqual(await readdir(join(state.dotnetRoot, 'sdk')), [sdkVersion])
  assert.deepEqual(await readdir(join(state.dotnetRoot, 'packs', 'Microsoft.NETCore.App.Ref')), [
    refPackVersion,
  ])
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

test('rejects changed, extra, and missing reference-pack files before either swap', async t => {
  const changed = await fixture(t)
  await writeFile(changed.referenceFile, 'changed reference bytes\n')
  const sdkBefore = (await stat(changed.file, { bigint: true })).mtimeNs
  assert.throws(() => materializeSdkVersion(changed), /files\/types\/bytes differ/)
  assert.equal((await stat(changed.file, { bigint: true })).mtimeNs, sdkBefore)

  const extra = await fixture(t)
  const extraPath = join(extra.referencePack, 'unexpected.dll')
  await writeFile(extraPath, 'extra')
  const extraSdkBefore = (await stat(extra.file, { bigint: true })).mtimeNs
  assert.throws(() => materializeSdkVersion(extra), /files\/types\/bytes differ/)
  assert.equal((await stat(extra.file, { bigint: true })).mtimeNs, extraSdkBefore)
  assert.equal(await readFile(extraPath, 'utf8'), 'extra')

  const missing = await fixture(t)
  await rm(missing.referenceFile)
  const missingSdkBefore = (await stat(missing.file, { bigint: true })).mtimeNs
  assert.throws(() => materializeSdkVersion(missing), /files\/types\/bytes differ/)
  assert.equal((await stat(missing.file, { bigint: true })).mtimeNs, missingSdkBefore)
})

test('rejects reference-pack mode drift and symlink before changing SDK', async t => {
  const mode = await fixture(t)
  await chmod(mode.referenceFile, 0o755)
  const modeSdkBefore = (await stat(mode.file, { bigint: true })).mtimeNs
  assert.throws(() => materializeSdkVersion(mode), /files\/types\/bytes differ/)
  assert.equal((await stat(mode.file, { bigint: true })).mtimeNs, modeSdkBefore)

  const linked = await fixture(t)
  await rm(linked.referenceFile)
  await symlink(linked.other, linked.referenceFile)
  const linkSdkBefore = (await stat(linked.file, { bigint: true })).mtimeNs
  assert.throws(() => materializeSdkVersion(linked), /link or special file/)
  assert.equal((await stat(linked.file, { bigint: true })).mtimeNs, linkSdkBefore)
})

test('rejects missing pinned reference-pack archive root before either swap', async t => {
  const state = await fixture(t, { omitArchivePack: true })
  const sdkBefore = (await stat(state.file, { bigint: true })).mtimeNs
  const packBefore = (await stat(state.referenceFile, { bigint: true })).mtimeNs
  assert.throws(() => materializeSdkVersion(state), /archive subtree missing/)
  assert.equal((await stat(state.file, { bigint: true })).mtimeNs, sdkBefore)
  assert.equal((await stat(state.referenceFile, { bigint: true })).mtimeNs, packBefore)
})

test('rolls back the SDK swap when reference-pack install rename fails', async t => {
  const state = await fixture(t)
  const sdkBefore = (await stat(state.file, { bigint: true })).mtimeNs
  const packBefore = (await stat(state.referenceFile, { bigint: true })).mtimeNs
  const helper = new URL(
    '../dotnet-clients/tooling/materialize-sdk-version-metadata.py',
    import.meta.url,
  ).pathname
  const result = spawnSync(
    'python3',
    [
      '-I',
      '-c',
      `import importlib.util,os,sys
helper,root,archive,digest=sys.argv[1:]
spec=importlib.util.spec_from_file_location('materializer',helper)
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
rename=os.rename
def fail_second_install(source,target):
 if source.endswith('/new-1') and target.endswith('/10.0.12'):
  raise OSError('injected second-root rename failure')
 return rename(source,target)
os.rename=fail_second_install
sys.argv=[helper,root,archive,digest]
module.main()`,
      helper,
      state.dotnetRoot,
      state.archivePath,
      state.archiveSha512,
    ],
    { encoding: 'utf8' },
  )
  assert.notEqual(result.status, 0)
  assert.match(result.stderr, /injected second-root rename failure/)
  assert.equal((await stat(state.file, { bigint: true })).mtimeNs, sdkBefore)
  assert.equal((await stat(state.referenceFile, { bigint: true })).mtimeNs, packBefore)
  assert.equal(await readFile(state.file, 'utf8'), content)
  assert.equal(await readFile(state.referenceFile, 'utf8'), referenceContent)
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
  assert.equal(refPackVersion, '10.0.12')
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
