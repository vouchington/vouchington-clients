import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'

const script = new URL('../scripts/ios-release-version.mjs', import.meta.url)

describe('iOS release version', () => {
  it('keeps XcodeGen version inputs and the generated plist variable-backed', () => {
    const project = readFileSync(
      new URL('../swift-clients/apps/iOS/project.yml', import.meta.url),
      'utf8',
    )
    const plist = readFileSync(
      new URL('../swift-clients/apps/iOS/Info.plist', import.meta.url),
      'utf8',
    )
    assert.match(project, /^\s+CFBundleShortVersionString: '\$\(MARKETING_VERSION\)'$/m)
    assert.match(project, /^\s+CFBundleVersion: '\$\(CURRENT_PROJECT_VERSION\)'$/m)
    assert.match(
      plist,
      /<key>CFBundleShortVersionString<\/key>\s*<string>\$\(MARKETING_VERSION\)<\/string>/,
    )
    assert.match(
      plist,
      /<key>CFBundleVersion<\/key>\s*<string>\$\(CURRENT_PROJECT_VERSION\)<\/string>/,
    )
  })

  it('uses a release tag and a unique build number for each run attempt', () => {
    assert.equal(
      execFileSync(process.execPath, [script.pathname, 'ios-v2.10.3', '1234', '2'], {
        encoding: 'utf8',
      }),
      'version=2.10.3\nbuild=1234.2.0\n',
    )
  })

  it('rejects non-release tags and ambiguous numbers', () => {
    for (const args of [
      ['v1.0.0', '1', '1'],
      ['ios-v1.02.0', '1', '1'],
      ['ios-v1.0', '1', '1'],
      ['ios-v1.0.0', '0', '1'],
      ['ios-v1.0.0', '1', '0'],
      ['ios-v1.0.0', '10000', '1'],
      ['ios-v1.0.0', '1', '100'],
    ]) {
      assert.notEqual(spawnSync(process.execPath, [script.pathname, ...args]).status, 0)
    }
  })
})
