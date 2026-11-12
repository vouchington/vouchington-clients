import assert from 'node:assert/strict'
import {
  copyFileSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  renameSync,
  rmSync,
  writeFileSync,
} from 'node:fs'
import { spawnSync } from 'node:child_process'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import { describe, it } from 'node:test'
import {
  checkArtifact,
  makeArtifact,
  sentryManifest,
  sentryReasons,
  verifier,
} from './helpers/apple-privacy-fixtures.mjs'

describe('Apple privacy release artifacts', () => {
  it('accepts iOS app, archive, and macOS bundle layouts', () => {
    const { directory, app } = makeArtifact()
    try {
      assert.equal(checkArtifact(app).status, 0)
      const applications = join(directory, 'Voucha.xcarchive/Products/Applications')
      mkdirSync(applications, { recursive: true })
      renameSync(app, join(applications, 'Voucha.app'))
      assert.equal(checkArtifact(join(directory, 'Voucha.xcarchive')).status, 0)
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }

    const mac = makeArtifact('macos')
    try {
      const result = checkArtifact(mac.app)
      assert.equal(result.status, 0, result.stderr)
    } finally {
      rmSync(mac.directory, { recursive: true, force: true })
    }
  })

  it('rejects unsupported artifacts, missing apps, and ambiguous archive contents', () => {
    for (const path of ['/tmp/Voucha.zip', '/tmp/Missing.app']) {
      assert.notEqual(
        spawnSync('python3', [verifier, '--artifact', path], { encoding: 'utf8' }).status,
        0,
      )
    }
    const directory = mkdtempSync(join(tmpdir(), 'apple-privacy-archive-'))
    try {
      const archive = join(directory, 'Voucha.xcarchive')
      const applications = join(archive, 'Products/Applications')
      mkdirSync(applications, { recursive: true })
      assert.notEqual(checkArtifact(archive).status, 0)
      mkdirSync(join(applications, 'One.app'))
      mkdirSync(join(applications, 'Two.app'))
      assert.notEqual(checkArtifact(archive).status, 0)
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })

  it('rejects missing and duplicate app/package manifests', () => {
    for (const mutate of [
      app => rmSync(join(app, 'PrivacyInfo.xcprivacy')),
      app => {
        const duplicate = join(app, 'Contents/Resources')
        mkdirSync(duplicate, { recursive: true })
        copyFileSync(join(app, 'PrivacyInfo.xcprivacy'), join(duplicate, 'PrivacyInfo.xcprivacy'))
      },
      app => rmSync(join(app, 'VouchaCore_VouchaAPI.bundle/PrivacyInfo.xcprivacy')),
      app => rmSync(join(app, 'VouchaUI_VouchaFeatures.bundle'), { recursive: true }),
      app => {
        const duplicate = join(app, 'Extra/VouchaCore_VouchaCore.bundle')
        mkdirSync(duplicate, { recursive: true })
        copyFileSync(
          join(app, 'VouchaCore_VouchaCore.bundle/PrivacyInfo.xcprivacy'),
          join(duplicate, 'PrivacyInfo.xcprivacy'),
        )
      },
    ]) {
      const { directory, app } = makeArtifact()
      try {
        mutate(app)
        assert.notEqual(checkArtifact(app).status, 0)
      } finally {
        rmSync(directory, { recursive: true, force: true })
      }
    }
  })

  it('rejects iOS-style locations inside a macOS app bundle', () => {
    for (const mutate of [
      app => {
        const nested = join(app, 'Contents/Resources/PrivacyInfo.xcprivacy')
        copyFileSync(nested, join(app, 'PrivacyInfo.xcprivacy'))
        rmSync(nested)
      },
      app => {
        const nested = join(
          app,
          'Contents/Resources/VouchaCore_VouchaCore.bundle/Contents/Resources/PrivacyInfo.xcprivacy',
        )
        const misplaced = join(
          app,
          'Contents/Resources/VouchaCore_VouchaCore.bundle/PrivacyInfo.xcprivacy',
        )
        copyFileSync(nested, misplaced)
        rmSync(nested)
      },
    ]) {
      const { directory, app } = makeArtifact('macos')
      try {
        mutate(app)
        assert.notEqual(checkArtifact(app).status, 0)
      } finally {
        rmSync(directory, { recursive: true, force: true })
      }
    }
  })

  it('requires a real Sentry framework manifest with the pinned required reasons', () => {
    const manifestPath = 'Frameworks/Sentry.framework/PrivacyInfo.xcprivacy'
    for (const mutate of [
      app => rmSync(join(app, manifestPath)),
      app => writeFileSync(join(app, manifestPath), '<?xml version="1.0"?><plist><dict>'),
      app => writeFileSync(join(app, manifestPath), '<?xml version="1.0"?><plist><array/></plist>'),
      app => writeFileSync(join(app, manifestPath), '<?xml version="1.0"?><plist><dict/></plist>'),
      app =>
        writeFileSync(
          join(app, manifestPath),
          sentryManifest([...Object.entries(sentryReasons), Object.entries(sentryReasons)[0]]),
        ),
      app => {
        rmSync(join(app, 'Frameworks/Sentry.framework'), { recursive: true })
        const decoy = join(app, 'NotSentry.framework')
        mkdirSync(decoy)
        writeFileSync(
          join(decoy, 'PrivacyInfo.xcprivacy'),
          sentryManifest(Object.entries(sentryReasons)),
        )
      },
    ]) {
      const { directory, app } = makeArtifact()
      try {
        mutate(app)
        const result = checkArtifact(app)
        assert.notEqual(result.status, 0)
        assert.doesNotMatch(result.stderr, /Traceback/u)
      } finally {
        rmSync(directory, { recursive: true, force: true })
      }
    }
  })

  it('rejects a well-formed Sentry manifest with an unsupported reason code', () => {
    const { directory, app } = makeArtifact()
    try {
      const manifest = join(app, 'Frameworks/Sentry.framework/PrivacyInfo.xcprivacy')
      writeFileSync(
        manifest,
        sentryManifest([
          ...Object.entries(sentryReasons).filter(
            ([category]) => category !== 'NSPrivacyAccessedAPICategoryFileTimestamp',
          ),
          ['NSPrivacyAccessedAPICategoryFileTimestamp', ['C617.2']],
        ]),
      )
      const result = checkArtifact(app)
      assert.notEqual(result.status, 0)
      assert.match(
        result.stderr,
        /does not contain the pinned Sentry required-reason declarations/u,
      )
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })

  it('rejects malformed app manifests and mismatched reason or collection policy', () => {
    for (const mutate of [
      app =>
        writeFileSync(join(app, 'PrivacyInfo.xcprivacy'), '<?xml version="1.0"?><plist><dict>'),
      app =>
        writeFileSync(
          join(app, 'PrivacyInfo.xcprivacy'),
          '<?xml version="1.0"?><plist><array/></plist>',
        ),
      app => {
        const path = join(app, 'PrivacyInfo.xcprivacy')
        writeFileSync(
          path,
          readFileSync(path, 'utf8').replace('<string>CA92.1</string>', '<string>CA92.2</string>'),
        )
      },
      app => {
        const path = join(app, 'PrivacyInfo.xcprivacy')
        writeFileSync(
          path,
          readFileSync(path, 'utf8').replace(
            '<key>NSPrivacyTracking</key>\n\t<false/>',
            '<key>NSPrivacyTracking</key>\n\t<integer>0</integer>',
          ),
        )
      },
      app => {
        const path = join(app, 'PrivacyInfo.xcprivacy')
        writeFileSync(
          path,
          readFileSync(path, 'utf8').replace(
            '<key>NSPrivacyCollectedDataTypeTracking</key>\n\t\t\t<false/>',
            '<key>NSPrivacyCollectedDataTypeTracking</key>\n\t\t\t<true/>',
          ),
        )
      },
      app => {
        const path = join(app, 'PrivacyInfo.xcprivacy')
        writeFileSync(
          path,
          readFileSync(path, 'utf8').replace(
            '<string>NSPrivacyCollectedDataTypePurposeAnalytics</string>',
            '<string>NSPrivacyCollectedDataTypePurposeAdvertising</string>',
          ),
        )
      },
    ]) {
      const { directory, app } = makeArtifact()
      try {
        mutate(app)
        const result = checkArtifact(app)
        assert.notEqual(result.status, 0)
        assert.doesNotMatch(result.stderr, /Traceback/u)
      } finally {
        rmSync(directory, { recursive: true, force: true })
      }
    }
  })

  it('requires an explicit checker mode', () => {
    const result = spawnSync('python3', [verifier], { encoding: 'utf8' })
    assert.equal(result.status, 2)
  })
})
