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
    directory('Products/')
    if kind == 'valid':
        directory('Products/Framework.framework/')
        directory('Products/Framework.framework/Versions/')
        directory('Products/Framework.framework/Versions/A/')
        directory('Products/Framework.framework/Versions/A/Resources/')
        file('Products/Framework.framework/Versions/A/Framework')
        entry = tarfile.TarInfo('Products/Framework.framework/Versions/Current')
        entry.type = tarfile.SYMTYPE
        entry.linkname = 'A'
        tar.addfile(entry)
        entry = tarfile.TarInfo('Products/Framework.framework/Resources')
        entry.type = tarfile.SYMTYPE
        entry.linkname = 'Versions/Current/Resources'
        tar.addfile(entry)
    elif kind == 'symlink-chain':
        directory('Products/dir/')
        directory('Products/dir/deep/')
        entry = tarfile.TarInfo('Products/dir/deep/go')
        entry.type = tarfile.SYMTYPE
        entry.linkname = '../..'
        tar.addfile(entry)
        entry = tarfile.TarInfo('Products/dir/deep/up')
        entry.type = tarfile.SYMTYPE
        entry.linkname = 'go/../../..'
        tar.addfile(entry)
    elif kind == 'duplicate':
        file('Products/same')
        file('Products/same')
    elif kind == 'conflicting':
        file('Products/file')
        file('Products/file/child')
    elif kind in ('escaping-link', 'absolute-link', 'hardlink-escape'):
        entry = tarfile.TarInfo('Products/Framework.framework/Current')
        entry.type = tarfile.LNKTYPE if kind == 'hardlink-escape' else tarfile.SYMTYPE
        entry.linkname = '/tmp/outside' if kind == 'absolute-link' else '../../../outside'
        tar.addfile(entry)
    elif kind == 'device':
        entry = tarfile.TarInfo('Products/device')
        entry.type = tarfile.CHRTYPE
        tar.addfile(entry)
    else:
        file('Products/../../outside' if kind == 'traversal' else '/tmp/outside')
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
