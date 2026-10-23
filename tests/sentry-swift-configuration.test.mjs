import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import { describe, it } from 'node:test'

const root = resolve(import.meta.dirname, '..')

describe('Swift Sentry configuration', () => {
  for (const platform of ['iOS', 'macOS']) {
    it(`keeps an empty ${platform} DSN and declares linked crash and performance data`, async () => {
      const project = await readFile(
        join(root, 'swift-clients/apps', platform, 'project.yml'),
        'utf8',
      )
      const info = await readFile(join(root, 'swift-clients/apps', platform, 'Info.plist'), 'utf8')
      const privacy = await readFile(
        join(root, 'swift-clients/apps', platform, 'PrivacyInfo.xcprivacy'),
        'utf8',
      )
      assert.match(project, /VOUCHA_SENTRY_DSN: ''/u)
      assert.match(project, /exactVersion: 9\.31\.0/u)
      assert.match(project, /product: Sentry/u)
      assert.doesNotMatch(project, /sentry-release-dsn/u)
      assert.match(info, /<key>VOUCHA_SENTRY_DSN<\/key>/u)
      for (const dataType of ['CrashData', 'PerformanceData']) {
        const entry = privacy.slice(privacy.indexOf(`NSPrivacyCollectedDataType${dataType}`))
        assert.match(entry, /NSPrivacyCollectedDataTypeLinked<\/key>\s*<true\/>/u)
        assert.match(entry, /NSPrivacyCollectedDataTypeTracking<\/key>\s*<false\/>/u)
      }
    })
  }

  it('pins sentry-cocoa in the generated app package lock', async () => {
    const generator = await readFile(join(root, 'swift-clients/tooling/generate.sh'), 'utf8')
    assert.match(generator, /sentry-cocoa/u)
    assert.match(generator, /2f64c77f77514acd01780d1aac86ae75b7fccd04/u)
    assert.match(generator, /9\.31\.0/u)
  })
})
