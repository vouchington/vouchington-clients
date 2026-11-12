import { copyFileSync, mkdirSync, mkdtempSync, writeFileSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
import { join } from 'node:path'
import { tmpdir } from 'node:os'

export const root = new URL('../..', import.meta.url).pathname
export const verifier = join(root, 'scripts/verify-apple-privacy.py')

export const packageManifests = {
  VouchaCore_VouchaCore: 'swift-clients/core/Sources/VouchaCore/PrivacyInfo.xcprivacy',
  VouchaCore_VouchaAPI: 'swift-clients/core/Sources/VouchaAPI/PrivacyInfo.xcprivacy',
  VouchaUI_VouchaFeatures: 'swift-clients/ui/Sources/VouchaFeatures/PrivacyInfo.xcprivacy',
}

export const sentryReasons = {
  NSPrivacyAccessedAPICategoryUserDefaults: ['CA92.1'],
  NSPrivacyAccessedAPICategorySystemBootTime: ['35F9.1'],
  NSPrivacyAccessedAPICategoryFileTimestamp: ['C617.1'],
}

export const sentryManifest = entries =>
  `<?xml version="1.0"?><plist version="1.0"><dict><key>NSPrivacyAccessedAPITypes</key><array>${entries
    .map(
      ([category, reasons]) =>
        `<dict><key>NSPrivacyAccessedAPIType</key><string>${category}</string><key>NSPrivacyAccessedAPITypeReasons</key><array>${reasons
          .map(reason => `<string>${reason}</string>`)
          .join('')}</array></dict>`,
    )
    .join('')}</array></dict></plist>`

export const makeArtifact = (layout = 'ios') => {
  const directory = mkdtempSync(join(tmpdir(), 'apple-privacy-'))
  const app = join(directory, 'Voucha.app')
  mkdirSync(app)
  const resources = layout === 'macos' ? join(app, 'Contents/Resources') : app
  mkdirSync(resources, { recursive: true })
  copyFileSync(
    join(root, `swift-clients/apps/${layout === 'macos' ? 'macOS' : 'iOS'}/PrivacyInfo.xcprivacy`),
    join(resources, 'PrivacyInfo.xcprivacy'),
  )
  for (const [product, source] of Object.entries(packageManifests)) {
    const resourceBundle = join(resources, `${product}.bundle`)
    const bundleResources =
      layout === 'macos' ? join(resourceBundle, 'Contents/Resources') : resourceBundle
    mkdirSync(bundleResources, { recursive: true })
    copyFileSync(join(root, source), join(bundleResources, 'PrivacyInfo.xcprivacy'))
  }
  const sentry = join(
    app,
    layout === 'macos'
      ? 'Contents/Frameworks/Sentry.framework/Versions/A/Resources'
      : 'Frameworks/Sentry.framework',
  )
  mkdirSync(sentry, { recursive: true })
  writeFileSync(
    join(sentry, 'PrivacyInfo.xcprivacy'),
    sentryManifest(Object.entries(sentryReasons)),
  )
  return { directory, app }
}

export const checkArtifact = app =>
  spawnSync('python3', [verifier, '--artifact', app, '--source-root', root], { encoding: 'utf8' })
