import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { test } from 'node:test'

const verifier = new URL('../scripts/verify-ios-test-tar.py', import.meta.url).pathname
const makeArchive = String.raw`
import io, sys, tarfile
archive, kind = sys.argv[1:]
with tarfile.open(archive, 'w:gz') as tar:
    def directory(name):
        entry = tarfile.TarInfo(name)
        entry.type = tarfile.DIRTYPE
        tar.addfile(entry)
    def file(name):
        entry = tarfile.TarInfo(name)
        entry.size = 1
        tar.addfile(entry, io.BytesIO(b'x'))
    directory('Voucha.xctestproducts/')
    if kind == 'valid':
        directory('Voucha.xctestproducts/Framework.framework/')
        directory('Voucha.xctestproducts/Framework.framework/Versions/')
        directory('Voucha.xctestproducts/Framework.framework/Versions/A/')
        directory('Voucha.xctestproducts/Framework.framework/Versions/A/Resources/')
        file('Voucha.xctestproducts/Framework.framework/Versions/A/Framework')
        entry = tarfile.TarInfo('Voucha.xctestproducts/Framework.framework/Versions/Current')
        entry.type = tarfile.SYMTYPE
        entry.linkname = 'A'
        tar.addfile(entry)
        entry = tarfile.TarInfo('Voucha.xctestproducts/Framework.framework/Resources')
        entry.type = tarfile.SYMTYPE
        entry.linkname = 'Versions/Current/Resources'
        tar.addfile(entry)
    elif kind == 'symlink-chain':
        directory('Voucha.xctestproducts/dir/')
        directory('Voucha.xctestproducts/dir/deep/')
        entry = tarfile.TarInfo('Voucha.xctestproducts/dir/deep/go')
        entry.type = tarfile.SYMTYPE
        entry.linkname = '../..'
        tar.addfile(entry)
        entry = tarfile.TarInfo('Voucha.xctestproducts/dir/deep/up')
        entry.type = tarfile.SYMTYPE
        entry.linkname = 'go/../../..'
        tar.addfile(entry)
    elif kind == 'duplicate':
        file('Voucha.xctestproducts/same')
        file('Voucha.xctestproducts/same')
    elif kind == 'conflicting':
        file('Voucha.xctestproducts/file')
        file('Voucha.xctestproducts/file/child')
    elif kind in ('escaping-link', 'absolute-link', 'hardlink-escape'):
        entry = tarfile.TarInfo('Voucha.xctestproducts/Framework.framework/Current')
        entry.type = tarfile.LNKTYPE if kind == 'hardlink-escape' else tarfile.SYMTYPE
        entry.linkname = '/tmp/outside' if kind == 'absolute-link' else '../../../outside'
        tar.addfile(entry)
    elif kind == 'device':
        entry = tarfile.TarInfo('Voucha.xctestproducts/device')
        entry.type = tarfile.CHRTYPE
        tar.addfile(entry)
    elif kind == 'appledouble':
        file('._Voucha.xctestproducts')
    else:
        file('Voucha.xctestproducts/../../outside' if kind == 'traversal' else '/tmp/outside')
`

test('iOS test archive accepts internal framework links and rejects escaping entries', () => {
  const root = mkdtempSync(join(tmpdir(), 'ios-test-tar-'))
  try {
    for (const [kind, accepted] of [
      ['valid', true],
      ['traversal', false],
      ['absolute-path', false],
      ['escaping-link', false],
      ['absolute-link', false],
      ['hardlink-escape', false],
      ['symlink-chain', false],
      ['duplicate', false],
      ['conflicting', false],
      ['device', false],
      ['appledouble', false],
    ]) {
      const archive = join(root, `${kind}.tar.gz`)
      execFileSync('python3', ['-c', makeArchive, archive, kind])
      const result = spawnSync('python3', [verifier, archive], { encoding: 'utf8' })
      assert.equal(result.status === 0, accepted, `${kind}: ${result.stderr}`)
    }
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('iOS test archive enforces compressed, expanded, and member limits before extraction', () => {
  const root = mkdtempSync(join(tmpdir(), 'ios-test-tar-limits-'))
  try {
    const archive = join(root, 'valid.tar.gz')
    execFileSync('python3', ['-c', makeArchive, archive, 'valid'])
    const script = String.raw`
import importlib.util, sys
spec = importlib.util.spec_from_file_location('archive_verifier', sys.argv[1])
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
setattr(module, sys.argv[3], int(sys.argv[4]))
module.validate(sys.argv[2])
`
    for (const [limit, expected] of [
      ['MAX_COMPRESSED_BYTES', 'compressed size limit'],
      ['MAX_UNCOMPRESSED_BYTES', 'uncompressed size limit'],
      ['MAX_MEMBERS', 'member limit'],
    ]) {
      const result = spawnSync('python3', ['-c', script, verifier, archive, limit, '0'], {
        encoding: 'utf8',
      })
      assert.notEqual(result.status, 0)
      assert.match(result.stderr, new RegExp(expected, 'u'))
    }
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('shared validator confines a selected SwiftPM product to .build', () => {
  const root = mkdtempSync(join(tmpdir(), 'swift-ui-test-tar-'))
  const probe = String.raw`
import io, importlib.util, sys, tarfile
verifier, archive, kind = sys.argv[1:]
with tarfile.open(archive, 'w:gz') as tar:
    for name, type_, target in [
        ('.build/', tarfile.DIRTYPE, ''),
        ('.build/arm64-apple-macosx/', tarfile.DIRTYPE, ''),
        ('.build/arm64-apple-macosx/debug/', tarfile.DIRTYPE, ''),
        ('.build/arm64-apple-macosx/debug/Tests.xctest/', tarfile.DIRTYPE, ''),
        ('.build/arm64-apple-macosx/debug/Tests.xctest/test', tarfile.REGTYPE, ''),
        ('.build/debug', tarfile.SYMTYPE, 'arm64-apple-macosx/debug'),
    ]:
        entry = tarfile.TarInfo(name)
        entry.type = type_
        entry.linkname = target
        if type_ == tarfile.REGTYPE:
            entry.size = 1
            tar.addfile(entry, io.BytesIO(b'x'))
        else:
            tar.addfile(entry)
    if kind != 'valid':
        entry = tarfile.TarInfo('.build/' + ('extra' if kind == 'extra' else 'escape'))
        entry.type = tarfile.REGTYPE if kind == 'extra' else tarfile.SYMTYPE
        entry.linkname = '' if kind == 'extra' else '../../outside'
        if kind == 'extra':
            entry.size = 1
            tar.addfile(entry, io.BytesIO(b'x'))
        else:
            tar.addfile(entry)
spec = importlib.util.spec_from_file_location('validator', verifier)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
module.validate(archive, root='.build', allowed_paths=(
    '.build/debug', '.build/arm64-apple-macosx/debug/Tests.xctest',
))
`
  try {
    for (const [kind, accepted] of [
      ['valid', true],
      ['extra', false],
      ['escape', false],
    ]) {
      const archive = join(root, `${kind}.tar.gz`)
      const result = spawnSync('python3', ['-c', probe, verifier, archive, kind], {
        encoding: 'utf8',
      })
      assert.equal(result.status === 0, accepted, `${kind}: ${result.stderr}`)
    }
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('Swift UI extraction rejects existing directories and symlinks at .build', () => {
  const script = String.raw`
import importlib.util, pathlib, sys, tempfile
spec = importlib.util.spec_from_file_location('ui_artifact', sys.argv[1])
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
with tempfile.TemporaryDirectory() as root:
    package = pathlib.Path(root)
    module.require_empty_build_root(package)
    (package / '.build').mkdir()
    try:
        module.require_empty_build_root(package)
    except ValueError:
        pass
    else:
        raise AssertionError('existing directory accepted')
    (package / '.build').rmdir()
    (package / '.build').symlink_to(package / 'missing')
    try:
        module.require_empty_build_root(package)
    except ValueError:
        pass
    else:
        raise AssertionError('dangling symlink accepted')
`
  execFileSync('python3', [
    '-c',
    script,
    new URL('../scripts/swift-ui-test-artifact.py', import.meta.url).pathname,
  ])
})
