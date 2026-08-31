import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

import {
  materializeSkipRepair,
  parseSkipUpdate,
  validateMaterializerSource,
  validateTrustedBaseDelta,
} from '../scripts/dependabot-android-repair.mjs'

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
    const upstream = 'package.targets += [.binaryTarget(name: "skip", url: "https://github.com/skiptools/skip/releases/download/1.2.3/skip-macos.zip", checksum: "' + 'c'.repeat(64) + '")]\n'
    assert.match(materializeSkipRepair(source, upstream, { revision, version: '1.2.3' }), /SKIP_VERSION="1.2.3"/u)
    assert.throws(() => materializeSkipRepair(source.replace('github.com', 'example.com'), upstream, { revision, version: '1.2.3' }))
  })

  it('requires the exact version-templated GitHub archive URL', () => {
    const source = 'SKIP_VERSION="1.2.3"\nSKIP_MACOS_ZIP_SHA256="' + 'a'.repeat(64) + '"\nSKIP_MACOS_GITHUB_ZIP_URL="https://github.com/skiptools/skip/releases/download/${SKIP_VERSION}/skip-macos.zip"\n'
    assert.doesNotThrow(() => validateMaterializerSource(source))
    assert.throws(() => validateMaterializerSource(source.replace('${SKIP_VERSION}', '1.2.3')))
    assert.throws(() => validateMaterializerSource(source.replace('github.com', 'example.com')))
    assert.throws(() => validateMaterializerSource(source.replace('\n', '\nSKIP_VERSION="1.2.3"\n')))
    assert.throws(() => validateMaterializerSource(source.replace(/^SKIP_MACOS_ZIP_SHA256=.*\n/mu, '')))
    assert.throws(() => validateMaterializerSource(source.replace('\nSKIP_MACOS_GITHUB', '\nSKIP_MACOS_ZIP_SHA256="' + 'b'.repeat(64) + '"\nSKIP_MACOS_GITHUB')))
  })

  it('permits only the executable Skip version delta and freezes existing resolved pins', () => {
    const trustedPackage = '.package(url: "https://source.skip.tools/skip.git", exact: "1.2.2")\n'
    const retained = {
      identity: 'skip-fuse-ui',
      kind: 'remoteSourceControl',
      location: 'https://source.skip.tools/skip-fuse-ui.git',
      state: { revision: 'b'.repeat(40), version: '1.0.1' },
    }
    const trustedResolved = JSON.stringify({ originHash: 'e'.repeat(64), pins: [{ identity: 'skip', kind: 'remoteSourceControl', location: 'https://source.skip.tools/skip.git', state: { revision: 'c'.repeat(40), version: '1.2.2' } }, retained], version: 3 })
    const candidateResolved = JSON.stringify({ originHash: 'f'.repeat(64), pins: [{ identity: 'skip', kind: 'remoteSourceControl', location: 'https://source.skip.tools/skip.git', state: { revision, version: '1.2.3' } }, retained, { identity: 'opencombine', kind: 'remoteSourceControl', location: 'https://github.com/OpenSwiftUIProject/OpenCombine.git', state: { revision: 'd'.repeat(40), version: '0.15.1' } }], version: 3 })
    assert.doesNotThrow(() => validateTrustedBaseDelta(trustedPackage, trustedResolved, packageSource, candidateResolved))
    assert.throws(() => validateTrustedBaseDelta(trustedPackage, trustedResolved, `${packageSource}let injected = true\n`, candidateResolved))
    assert.throws(() => validateTrustedBaseDelta(trustedPackage, trustedResolved, packageSource, candidateResolved.replace('1.0.1', '1.0.2')))
  })
})
