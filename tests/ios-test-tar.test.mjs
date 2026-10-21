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
    root = tarfile.TarInfo('Products/')
    root.type = tarfile.DIRTYPE
    tar.addfile(root)
    entry = tarfile.TarInfo({
        'valid': 'Products/Framework.framework/Current',
        'traversal': 'Products/../../outside',
        'escaping-link': 'Products/Framework.framework/Current',
        'absolute-link': 'Products/Framework.framework/Current',
        'device': 'Products/device',
    }[kind])
    if kind in ('valid', 'escaping-link', 'absolute-link'):
        entry.type = tarfile.SYMTYPE
        entry.linkname = {
            'valid': 'Versions/A',
            'escaping-link': '../../../outside',
            'absolute-link': '/tmp/outside',
        }[kind]
        tar.addfile(entry)
    elif kind == 'device':
        entry.type = tarfile.CHRTYPE
        tar.addfile(entry)
    else:
        entry.size = 1
        tar.addfile(entry, io.BytesIO(b'x'))
`

test('iOS test archive accepts internal framework links and rejects escaping entries', () => {
  const root = mkdtempSync(join(tmpdir(), 'ios-test-tar-'))
  try {
    for (const [kind, accepted] of [
      ['valid', true],
      ['traversal', false],
      ['escaping-link', false],
      ['absolute-link', false],
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
