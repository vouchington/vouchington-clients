import assert from 'node:assert/strict'
import childProcess, { spawnSync } from 'node:child_process'
import { chmod, mkdir, mkdtemp, readFile, rm, stat, symlink, writeFile } from 'node:fs/promises'
import { syncBuiltinESMExports } from 'node:module'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  inventoryTrackedSourceMetadata,
  restoreTrackedSourceTimes,
} from '../swift-clients/apps/android/tooling/android-state-source-metadata.mjs'

function git(root, ...args) {
  const result = spawnSync('git', args, { cwd: root, encoding: 'utf8' })
  assert.equal(result.status, 0, result.stderr)
  return result.stdout.trim()
}
function exactTime(path, ns) {
  const result = spawnSync(
    'python3',
    ['-I', '-c', 'import os,sys; n=int(sys.argv[2]); os.utime(sys.argv[1], ns=(n,n))', path, ns],
    { encoding: 'utf8' },
  )
  assert.equal(result.status, 0, result.stderr)
}
function exactAccessAndModificationTime(path, accessNs, modificationNs) {
  const result = spawnSync(
    'python3',
    [
      '-I',
      '-c',
      'import os,sys; os.utime(sys.argv[1], ns=(int(sys.argv[2]),int(sys.argv[3])))',
      path,
      accessNs,
      modificationNs,
    ],
    { encoding: 'utf8' },
  )
  assert.equal(result.status, 0, result.stderr)
}
async function fixture(t) {
  const checkoutRoot = await mkdtemp(join(tmpdir(), 'android-source-metadata-'))
  t.after(() => rm(checkoutRoot, { recursive: true, force: true }))
  git(checkoutRoot, 'init', '-q')
  const first = join(checkoutRoot, 'swift-clients/core/Sources/a.swift')
  const second = join(checkoutRoot, 'swift-clients/apps/android/Package.swift')
  await mkdir(join(checkoutRoot, 'swift-clients/core/Sources'), { recursive: true })
  await mkdir(join(checkoutRoot, 'swift-clients/apps/android'), { recursive: true })
  await writeFile(first, 'public let a = 1\n')
  await writeFile(second, '// package\n')
  git(checkoutRoot, 'add', 'swift-clients')
  git(
    checkoutRoot,
    '-c',
    'user.name=Test',
    '-c',
    'user.email=test@example.invalid',
    'commit',
    '-qm',
    'fixture',
  )
  const identity = {
    revision: git(checkoutRoot, 'rev-parse', 'HEAD'),
    run: '123',
    attempt: '2',
    gradle: '9.4.1',
  }
  const limits = { entries: 20, bytes: 8192, metadataBytes: 8192, scanMs: 5000 }
  return { checkoutRoot, first, second, identity, limits }
}

async function androidSourcesFixture(t) {
  const state = await fixture(t)
  const source = join(
    state.checkoutRoot,
    'swift-clients/apps/android/Sources/VouchaAndroid/Feature.swift',
  )
  const asset = join(
    state.checkoutRoot,
    'swift-clients/apps/android/Sources/VouchaAndroid/Resources/Module.xcassets/Contents.json',
  )
  await mkdir(
    join(
      state.checkoutRoot,
      'swift-clients/apps/android/Sources/VouchaAndroid/Resources/Module.xcassets',
    ),
    { recursive: true },
  )
  await writeFile(source, 'public let feature = 1\n')
  await writeFile(asset, '{"info":{"version":1}}\n')
  git(state.checkoutRoot, 'add', 'swift-clients/apps/android/Sources')
  git(
    state.checkoutRoot,
    '-c',
    'user.name=Test',
    '-c',
    'user.email=test@example.invalid',
    'commit',
    '-qm',
    'source fixture',
  )
  return {
    ...state,
    source,
    asset,
    identity: { ...state.identity, revision: git(state.checkoutRoot, 'rev-parse', 'HEAD') },
  }
}

test('restores exact source nanoseconds only after identical tracked bytes', async t => {
  const state = await fixture(t)
  exactTime(state.first, '1792634209267950316')
  exactTime(state.second, '1792634209267950317')
  const metadata = await inventoryTrackedSourceMetadata(state)
  assert.deepEqual(
    metadata.entries.map(entry => entry.path),
    ['swift-clients/apps/android/Package.swift', 'swift-clients/core/Sources/a.swift'],
  )
  assert.equal(metadata.entries[1].mtimeNs, '1792634209267950316')
  exactAccessAndModificationTime(state.first, '2000000000000000000', '1792634209267950416')
  exactTime(state.second, '1792634209267950417')
  await restoreTrackedSourceTimes({ ...state, metadata })
  assert.equal(
    (await stat(state.first, { bigint: true })).mtimeNs.toString(),
    '1792634209267950316',
  )
  assert.equal(
    (await stat(state.first, { bigint: true })).atimeNs.toString(),
    '2000000000000000000',
  )
  assert.equal(
    (await stat(state.second, { bigint: true })).mtimeNs.toString(),
    '1792634209267950317',
  )
})

test('changed later file fails before earlier source mtime is modified', async t => {
  const state = await fixture(t)
  const metadata = await inventoryTrackedSourceMetadata(state)
  exactTime(state.second, '1792634209267950417')
  await writeFile(state.first, 'public let a = 2\n')
  const before = (await stat(state.second, { bigint: true })).mtimeNs
  await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata }), /bytes differ/)
  assert.equal((await stat(state.second, { bigint: true })).mtimeNs, before)
})

test('missing and newly tracked inputs invalidate the complete inventory', async t => {
  const state = await fixture(t)
  const metadata = await inventoryTrackedSourceMetadata(state)
  await rm(state.first)
  await assert.rejects(
    restoreTrackedSourceTimes({ ...state, metadata }),
    /No such file or directory/,
  )
  await writeFile(state.first, 'public let a = 1\n')
  const extra = join(state.checkoutRoot, 'swift-clients/ui/Sources/extra.swift')
  await mkdir(join(state.checkoutRoot, 'swift-clients/ui/Sources'), { recursive: true })
  await writeFile(extra, 'public let extra = 1\n')
  git(state.checkoutRoot, 'add', 'swift-clients/ui/Sources/extra.swift')
  await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata }), /source set differs/)
})

test('rejects symlinked tracked file or ancestor and leaves source alone', async t => {
  const state = await fixture(t)
  const metadata = await inventoryTrackedSourceMetadata(state)
  exactTime(state.second, '1792634209267950417')
  const untouched = (await stat(state.second, { bigint: true })).mtimeNs
  const outside = join(state.checkoutRoot, 'outside.swift')
  await writeFile(outside, await readFile(state.first))
  await rm(state.first)
  await symlink(outside, state.first)
  await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata }), /symbolic links/)
  assert.equal((await stat(state.second, { bigint: true })).mtimeNs, untouched)
  await rm(state.first)
  await writeFile(state.first, 'public let a = 1\n')
  const sourceDirectory = join(state.checkoutRoot, 'swift-clients/core/Sources')
  const moved = join(state.checkoutRoot, 'source-moved')
  const { rename } = await import('node:fs/promises')
  await rename(sourceDirectory, moved)
  await symlink(moved, sourceDirectory)
  await assert.rejects(
    restoreTrackedSourceTimes({ ...state, metadata }),
    /symbolic links|Not a directory/,
  )
  assert.equal((await stat(state.second, { bigint: true })).mtimeNs, untouched)
})

test('rejects identity, malformed metadata, and caps before touching files', async t => {
  const state = await fixture(t)
  const metadata = await inventoryTrackedSourceMetadata(state)
  const before = (await stat(state.first, { bigint: true })).mtimeNs
  await assert.rejects(
    restoreTrackedSourceTimes({ ...state, identity: { ...state.identity, run: '4' }, metadata }),
    /identity/,
  )
  await assert.rejects(
    restoreTrackedSourceTimes({
      ...state,
      metadata: {
        ...metadata,
        entries: [{ ...metadata.entries[0], mtimeNs: '01' }, ...metadata.entries.slice(1)],
      },
    }),
    /invalid metadata value/,
  )
  await assert.rejects(
    restoreTrackedSourceTimes({
      ...state,
      metadata: {
        ...metadata,
        entries: [{ ...metadata.entries[0], mtimeNs: 0 }, ...metadata.entries.slice(1)],
      },
    }),
    /invalid metadata value/,
  )
  await assert.rejects(
    restoreTrackedSourceTimes({
      ...state,
      metadata: { ...metadata, entries: [metadata.entries[0], metadata.entries[0]] },
    }),
    /duplicated or unordered/,
  )
  await assert.rejects(
    restoreTrackedSourceTimes({ ...state, metadata, limits: { ...state.limits, entries: 1 } }),
    /cap|bounds/,
  )
  assert.equal((await stat(state.first, { bigint: true })).mtimeNs, before)
})

test('restores genuine Android source directory nanoseconds across checkouts', async t => {
  const state = await androidSourcesFixture(t)
  const rootDirectory = join(state.checkoutRoot, 'swift-clients/apps/android/Sources')
  const assetDirectory = join(
    state.checkoutRoot,
    'swift-clients/apps/android/Sources/VouchaAndroid/Resources/Module.xcassets',
  )
  exactTime(rootDirectory, '1792634209267950316')
  exactTime(assetDirectory, '1792634209267950317')
  const metadata = await inventoryTrackedSourceMetadata(state)
  assert.equal(metadata.version, 2)
  assert.equal(metadata.directories.length, 4)
  assert.equal(metadata.directories[0].mtimeNs, '1792634209267950316')
  assert.equal(metadata.directories[3].mtimeNs, '1792634209267950317')
  const clone = await mkdtemp(join(tmpdir(), 'android-source-clone-'))
  t.after(() => rm(clone, { recursive: true, force: true }))
  git(clone, 'clone', '-q', '--no-hardlinks', state.checkoutRoot, '.')
  const cloneRoot = join(clone, 'swift-clients/apps/android/Sources')
  const cloneAsset = join(
    clone,
    'swift-clients/apps/android/Sources/VouchaAndroid/Resources/Module.xcassets',
  )
  exactAccessAndModificationTime(cloneRoot, '2000000000000000000', '1792634209267950416')
  exactAccessAndModificationTime(cloneAsset, '2000000000000000001', '1792634209267950417')
  await restoreTrackedSourceTimes({ ...state, checkoutRoot: clone, metadata })
  assert.equal((await stat(cloneRoot, { bigint: true })).mtimeNs.toString(), '1792634209267950316')
  assert.equal((await stat(cloneAsset, { bigint: true })).mtimeNs.toString(), '1792634209267950317')
  assert.equal((await stat(cloneRoot, { bigint: true })).atimeNs.toString(), '2000000000000000000')
  assert.equal((await stat(cloneAsset, { bigint: true })).atimeNs.toString(), '2000000000000000001')
  assert.equal(
    (
      await stat(join(clone, 'swift-clients/apps/android/Sources/VouchaAndroid/Feature.swift'), {
        bigint: true,
      })
    ).mtimeNs.toString(),
    metadata.entries.find(entry => entry.path.endsWith('/Feature.swift')).mtimeNs,
  )
})

test('late untracked child or changed source directory leaves earlier file and directory times untouched', async t => {
  const state = await androidSourcesFixture(t)
  const metadata = await inventoryTrackedSourceMetadata(state)
  const rootDirectory = join(state.checkoutRoot, 'swift-clients/apps/android/Sources')
  const assetDirectory = join(
    state.checkoutRoot,
    'swift-clients/apps/android/Sources/VouchaAndroid/Resources/Module.xcassets',
  )
  exactTime(state.second, '1792634209267950416')
  exactTime(rootDirectory, '1792634209267950417')
  const beforeFile = (await stat(state.second, { bigint: true })).mtimeNs
  const beforeDirectory = (await stat(rootDirectory, { bigint: true })).mtimeNs
  const extra = join(assetDirectory, 'untracked.json')
  await writeFile(extra, '{}\n')
  await assert.rejects(inventoryTrackedSourceMetadata(state), /graph or mode differs/)
  await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata }), /graph or mode differs/)
  assert.equal((await stat(state.second, { bigint: true })).mtimeNs, beforeFile)
  assert.equal((await stat(rootDirectory, { bigint: true })).mtimeNs, beforeDirectory)
  await rm(extra)
  await chmod(assetDirectory, 0o700)
  await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata }), /graph or mode differs/)
  assert.equal((await stat(state.second, { bigint: true })).mtimeNs, beforeFile)
  await chmod(assetDirectory, 0o755)
  const link = join(assetDirectory, 'escaped.json')
  await symlink(state.second, link)
  await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata }), /link or special member/)
  assert.equal((await stat(state.second, { bigint: true })).mtimeNs, beforeFile)
})

test('source directory graph rejects symlink, missing member, and malformed metadata before mutation', async t => {
  const state = await androidSourcesFixture(t)
  const metadata = await inventoryTrackedSourceMetadata(state)
  exactTime(state.second, '1792634209267950416')
  const before = (await stat(state.second, { bigint: true })).mtimeNs
  const changed = {
    ...metadata,
    directories: metadata.directories.map((entry, index) =>
      index === 0 ? { ...entry, childrenSha256: '0'.repeat(64) } : entry,
    ),
  }
  await assert.rejects(
    restoreTrackedSourceTimes({ ...state, metadata: changed }),
    /graph or metadata invalid/,
  )
  const old = { ...metadata, version: 1 }
  await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata: old }), /schema/)
  const invalidPath = {
    ...metadata,
    directories: metadata.directories.map((entry, index) =>
      index === 0 ? { ...entry, path: '../Sources' } : entry,
    ),
  }
  await assert.rejects(
    restoreTrackedSourceTimes({ ...state, metadata: invalidPath }),
    /graph or metadata invalid/,
  )
  const asset = state.asset
  await rm(asset)
  await assert.rejects(
    restoreTrackedSourceTimes({ ...state, metadata }),
    /No such file or directory/,
  )
  await symlink(state.second, asset)
  await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata }), /symbolic links/)
  assert.equal((await stat(state.second, { bigint: true })).mtimeNs, before)
})

function replayAfterValidatedFileMutation(state, metadata, mutation) {
  const helper = fileURLToPath(
    new URL('../swift-clients/apps/android/tooling/android-state-source-ns.py', import.meta.url),
  )
  const script = `
import os,runpy,shutil,sys
helper,mutation=sys.argv[1:]
original_open=os.open
changed=False
def replace_after_open(path, flags, *args, **kwargs):
    global changed
    fd=original_open(path, flags, *args, **kwargs)
    if path == 'a.swift' and not changed:
        changed=True
        parent='swift-clients/apps/android'
        file=parent+'/Package.swift'
        if mutation in ('parent-directory', 'parent-symlink'):
            os.rename(parent, 'detached-parent')
            if mutation == 'parent-symlink':
                os.symlink('../../detached-parent', parent)
            else:
                os.mkdir(parent)
                shutil.copy2('detached-parent/Package.swift', file)
        elif mutation == 'file-symlink':
            os.rename(file, 'detached-file')
            os.symlink('../../../detached-file', file)
        elif mutation == 'file-replacement':
            os.rename(file, 'detached-file')
            shutil.copy2('detached-file', file)
        elif mutation == 'file-mode':
            os.chmod(file, (os.stat(file).st_mode & 0o777) ^ 0o100)
        elif mutation == 'file-modified':
            with open(file, 'r+b') as output:
                output.write(b'// changed')
            info=os.stat(file)
            os.utime(file, ns=(info.st_atime_ns, 1792634209267950518))
    return fd
os.open=replace_after_open
sys.argv=[helper]
runpy.run_path(helper, run_name='__main__')
`
  return spawnSync('python3', ['-I', '-c', script, helper, mutation], {
    cwd: state.checkoutRoot,
    input: JSON.stringify({ entries: metadata.entries, directories: metadata.directories }),
    encoding: 'utf8',
    timeout: 5000,
  })
}

for (const mutation of [
  'parent-directory',
  'parent-symlink',
  'file-symlink',
  'file-replacement',
  'file-mode',
  'file-modified',
]) {
  test(`rejects ${mutation} after an earlier file was hashed before any timestamp replay`, async t => {
    const state = await fixture(t)
    const metadata = await inventoryTrackedSourceMetadata(state)
    exactTime(state.first, '1792634209267950416')
    exactTime(state.second, '1792634209267950417')
    const result = replayAfterValidatedFileMutation(state, metadata, mutation)
    assert.ifError(result.error)
    assert.notEqual(result.status, 0, result.stderr)
    assert.match(result.stderr, /changed before replay|symbolic links|Not a directory/)
    assert.equal(
      (await stat(state.first, { bigint: true })).mtimeNs.toString(),
      '1792634209267950416',
    )
    const earlier = mutation.startsWith('parent-')
      ? join(state.checkoutRoot, 'detached-parent/Package.swift')
      : mutation === 'file-replacement' || mutation === 'file-symlink'
        ? join(state.checkoutRoot, 'detached-file')
        : state.second
    const expected = mutation === 'file-modified' ? '1792634209267950518' : '1792634209267950417'
    assert.equal((await stat(earlier, { bigint: true })).mtimeNs.toString(), expected)
  })
}

for (const stderr of [
  'Android source metadata progress: {"phase":"replay","files":2,"applied":1}',
  '',
]) {
  test(`timestamp timeout retains ${stderr ? 'last helper phase' : 'missing helper progress'} and workload context`, async t => {
    const state = await fixture(t)
    const metadata = await inventoryTrackedSourceMetadata(state)
    const before = (await stat(state.first, { bigint: true })).mtimeNs
    const original = childProcess.spawnSync
    let pythonCalls = 0
    t.mock.method(childProcess, 'spawnSync', (command, args, options) => {
      if (command !== 'python3') return original(command, args, options)
      pythonCalls++
      return { error: new Error('spawnSync python3 ETIMEDOUT'), status: null, stdout: '', stderr }
    })
    syncBuiltinESMExports()
    try {
      await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata }), error => {
        assert.match(error.message, /exact timestamp replay failed after [0-9]+ms/)
        assert.match(
          error.message,
          /2 files, 0 directories, [0-9]+ input bytes, [0-9]+ms helper budget/,
        )
        assert.match(error.message, /spawnSync python3 ETIMEDOUT/)
        if (stderr) assert.ok(error.message.includes(stderr))
        else assert.match(error.message, /helper reported no progress/)
        return true
      })
    } finally {
      t.mock.restoreAll()
      syncBuiltinESMExports()
    }
    assert.equal(pythonCalls, 1)
    assert.equal((await stat(state.first, { bigint: true })).mtimeNs, before)
  })
}

test('one batched replay reports bounded phases with verified file, byte and applied counts', async t => {
  const state = await fixture(t)
  const metadata = await inventoryTrackedSourceMetadata(state)
  const helper = fileURLToPath(
    new URL('../swift-clients/apps/android/tooling/android-state-source-ns.py', import.meta.url),
  )
  const result = spawnSync('python3', ['-I', helper], {
    cwd: state.checkoutRoot,
    input: JSON.stringify({ entries: metadata.entries, directories: metadata.directories }),
    encoding: 'utf8',
    timeout: 5000,
  })
  assert.ifError(result.error)
  assert.equal(result.status, 0, result.stderr)
  const events = result.stderr
    .trim()
    .split('\n')
    .map(line => JSON.parse(line.replace('Android source metadata progress: ', '')))
  assert.deepEqual(
    events.map(event => event.phase),
    ['validation', 'revalidation', 'replay', 'complete'],
  )
  const summary = events.at(-1)
  assert.equal(summary.files, metadata.entries.length)
  assert.equal(summary.expectedFiles, metadata.entries.length)
  assert.equal(summary.directories, metadata.directories.length)
  assert.equal(summary.expectedDirectories, metadata.directories.length)
  assert.equal(
    summary.bytes,
    metadata.entries.reduce((total, entry) => total + entry.size, 0),
  )
  assert.equal(summary.revalidatedFiles, metadata.entries.length)
  assert.equal(summary.applied, metadata.entries.length + metadata.directories.length)
  assert.equal(summary.rolledBack, 0)
  assert.deepEqual(Object.keys(summary.phaseMs), [
    'startup',
    'validation',
    'revalidation',
    'replay',
  ])
})
