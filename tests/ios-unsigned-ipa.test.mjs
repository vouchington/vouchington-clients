import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'

const script = new URL('../scripts/ios-unsigned-ipa.mjs', import.meta.url)
const workflow = readFileSync(
  new URL('../.github/workflows/release-ios-testflight.yml', import.meta.url),
  'utf8',
)

const plist = ({ version = '2.10.3', build = '1234.2.0', bundleId = 'ai.voucha.ios' } = {}) =>
  `<?xml version="1.0" encoding="UTF-8"?>
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>${bundleId}</string>
<key>CFBundleShortVersionString</key><string>${version}</string>
<key>CFBundleVersion</key><string>${build}</string>
</dict></plist>`

const writeIpa = entries => {
  const directory = mkdtempSync(join(tmpdir(), 'ios-ipa-'))
  for (const [name, body] of Object.entries(entries)) {
    const path = join(directory, name)
    mkdirSync(join(path, '..'), { recursive: true })
    writeFileSync(path, body)
  }
  const ipa = join(directory, 'Voucha-unsigned.ipa')
  const roots = [...new Set(Object.keys(entries).map(name => name.split('/')[0]))]
  execFileSync('zip', ['-qr', '-X', ipa, ...roots], { cwd: directory })
  return { directory, ipa }
}

const check = (ipa, args = ['2.10.3', '1234.2.0', 'ai.voucha.ios']) =>
  spawnSync(process.execPath, [script.pathname, ipa, ...args], { encoding: 'utf8' })

describe('unsigned iOS inspection IPA', () => {
  it('accepts one Payload app with the release identity', () => {
    const packed = writeIpa({
      'Payload/Voucha.app/Info.plist': plist(),
      'Payload/Voucha.app/Voucha': 'arm64',
    })
    try {
      const result = check(packed.ipa)
      assert.equal(result.status, 0, result.stderr)
    } finally {
      rmSync(packed.directory, { recursive: true, force: true })
    }
  })

  it('rejects a binary Info.plist', () => {
    const packed = writeIpa({ 'Payload/Voucha.app/Info.plist': 'bplist00not-xml' })
    try {
      const result = check(packed.ipa)
      assert.notEqual(result.status, 0)
      assert.match(result.stderr, /must be XML/u)
    } finally {
      rmSync(packed.directory, { recursive: true, force: true })
    }
  })

  it('rejects a layout that is not an unsigned inspection IPA', () => {
    const cases = [
      { 'Payload/Voucha.app/Voucha': 'arm64' },
      {
        'Payload/Voucha.app/Info.plist': plist(),
        'Payload/Other.app/Info.plist': plist(),
      },
      {
        'Payload/Voucha.app/Info.plist': plist(),
        'Payload/Voucha.app/embedded.mobileprovision': 'profile',
      },
      {
        'Payload/Voucha.app/Info.plist': plist(),
        'SwiftSupport/iphoneos/libswiftCore.dylib': 'swift',
      },
      { 'Payload/Info.plist': plist() },
      { 'Payload/Voucha.app/Info.plist': plist({ version: '9.9.9' }) },
    ]
    for (const entries of cases) {
      const packed = writeIpa(entries)
      try {
        assert.notEqual(check(packed.ipa).status, 0)
      } finally {
        rmSync(packed.directory, { recursive: true, force: true })
      }
    }
    assert.notEqual(check('/nonexistent/Voucha-unsigned.ipa').status, 0)
  })

  it('keeps packaging inside the existing archive job and does not export or upload', () => {
    const archive = workflow.slice(
      workflow.indexOf('- name: Archive unsigned iOS app'),
      workflow.indexOf('- name: Package unsigned inspection IPA'),
    )
    const packaged = workflow.slice(
      workflow.indexOf('- name: Package unsigned inspection IPA'),
      workflow.indexOf('- name: Retain bounded archive failure diagnostics'),
    )
    assert.match(workflow, /^    timeout-minutes: 30$/m)
    assert.match(archive, /timeout-minutes: 23/u)
    assert.match(packaged, /timeout-minutes: 3/u)
    assert.match(packaged, /ditto "\$app" "\$payload\/Voucha\.app"/u)
    assert.match(packaged, /ditto -c -k --keepParent --norsrc "\$payload"/u)
    assert.match(packaged, /Voucha-unsigned\.ipa/u)
    assert.match(packaged, /node scripts\/ios-unsigned-ipa\.mjs/u)
    assert.match(packaged, /ai\.voucha\.ios/u)
    assert.match(packaged, /lipo -archs "\$payload\/Voucha\.app\/Voucha"\)" = arm64/u)
    assert.match(packaged, /embedded\.mobileprovision/u)
    assert.match(packaged, /plutil -convert xml1 "\$payload\/Voucha\.app\/Info\.plist"/u)
    assert.match(packaged, /Signature=adhoc/u)
    assert.match(packaged, /TeamIdentifier=not set/u)
    assert.match(packaged, /code object is not signed at all/u)
    assert.doesNotMatch(workflow, /-exportArchive|altool|notarytool|iTMSTransporter|secrets\./u)
    assert.match(
      workflow,
      /path: \$\{\{ runner\.temp \}\}\/ios-unsigned-export\/\n {10}if-no-files-found: error\n {10}retention-days: 7/u,
    )
  })
})
