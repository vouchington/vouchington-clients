import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

import { materializeSkipRepair, parseSkipUpdate } from '../scripts/dependabot-android-repair.mjs'

const revision = 'a'.repeat(40)

describe('trusted Android Skip repair', () => {
  const packageSource = '.package(url: "https://source.skip.tools/skip.git", exact: "1.2.3")\n'
  const resolvedSource = JSON.stringify({ pins: [{ identity: 'skip', state: { revision, version: '1.2.3' } }] })

  it('derives only the declared exact Skip version and matching resolved pin', () => {
    assert.deepEqual(parseSkipUpdate(packageSource, resolvedSource), { revision, version: '1.2.3' })
    assert.throws(() => parseSkipUpdate(packageSource, resolvedSource.replace('1.2.3', '9.9.9')))
  })

  it('updates only trusted materializer fields from the verified upstream checksum', () => {
    const source = 'SKIP_VERSION="1.0.0"\nSKIP_MACOS_ZIP_SHA256="' + 'b'.repeat(64) + '"\nSKIP_MACOS_GITHUB_ZIP_URL="https://github.com/skiptools/skip/releases/download/${SKIP_VERSION}/skip-macos.zip"\n'
    const upstream = '.binaryTarget(name: "skip", url: "https://github.com/skiptools/skip/releases/download/1.2.3/skip-macos.zip", checksum: "' + 'c'.repeat(64) + '")\n'
    assert.match(materializeSkipRepair(source, upstream, { revision, version: '1.2.3' }), /SKIP_VERSION="1.2.3"/u)
    assert.throws(() => materializeSkipRepair(source.replace('github.com', 'example.com'), upstream, { revision, version: '1.2.3' }))
  })
})
