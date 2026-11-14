import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'

const readRepoFile = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8')

const plistElement = (xml, key) => {
  const match = xml.match(
    new RegExp(
      `<key>${key}</key>\\s*(<string>[\\s\\S]*?</string>|<array>[\\s\\S]*?</array>|<dict\\s*/>|<dict>[\\s\\S]*?</dict>)`,
      'u',
    ),
  )
  assert.ok(match, `missing Info.plist key ${key}`)
  return match[1]
}

const plistStrings = element =>
  [...element.matchAll(/<string>([^<]*)<\/string>/gu)].map(match => match[1])

const yamlStringList = (project, key) => {
  const match = project.match(
    new RegExp(`(?:^|\\n)[ \\t]*${key}:\\n((?:[ \\t]+-[ \\t]+[^\\n]+\\n)+)`, 'u'),
  )
  assert.ok(match, `missing project.yml list ${key}`)
  return [...match[1].matchAll(/^[ \t]+- (.+)$/gmu)].map(item => item[1].trim())
}

const iphoneOrientations = [
  'UIInterfaceOrientationPortrait',
  'UIInterfaceOrientationLandscapeLeft',
  'UIInterfaceOrientationLandscapeRight',
]
const ipadOrientations = [
  'UIInterfaceOrientationPortrait',
  'UIInterfaceOrientationPortraitUpsideDown',
  'UIInterfaceOrientationLandscapeLeft',
  'UIInterfaceOrientationLandscapeRight',
]

describe('in-app display metadata', () => {
  it('sets the iOS home-screen name, generated launch screen, and orientations', () => {
    const plist = readRepoFile('swift-clients/apps/iOS/Info.plist')
    const project = readRepoFile('swift-clients/apps/iOS/project.yml')

    assert.equal(plistElement(plist, 'CFBundleDisplayName'), '<string>Voucha</string>')
    assert.match(project, /CFBundleDisplayName: Voucha/u)
    assert.equal(plistElement(plist, 'UILaunchScreen'), '<dict/>')
    assert.match(project, /UILaunchScreen: \{\}/u)
    assert.doesNotMatch(plistElement(plist, 'UILaunchScreen'), /UIImageName|UIColorName/u)
    assert.deepEqual(
      plistStrings(plistElement(plist, 'UISupportedInterfaceOrientations')),
      iphoneOrientations,
    )
    assert.deepEqual(
      plistStrings(plistElement(plist, 'UISupportedInterfaceOrientations~ipad')),
      ipadOrientations,
    )
    assert.deepEqual(
      yamlStringList(project, 'UISupportedInterfaceOrientations'),
      iphoneOrientations,
    )
    assert.deepEqual(
      yamlStringList(project, '"UISupportedInterfaceOrientations~ipad"'),
      ipadOrientations,
    )
  })

  it('sets the macOS display name without iOS launch or orientation keys', () => {
    const plist = readRepoFile('swift-clients/apps/macOS/Info.plist')
    const project = readRepoFile('swift-clients/apps/macOS/project.yml')

    assert.equal(plistElement(plist, 'CFBundleDisplayName'), '<string>Voucha</string>')
    assert.match(project, /CFBundleDisplayName: Voucha/u)
    assert.doesNotMatch(plist, /<key>UILaunchScreen<\/key>/u)
    assert.doesNotMatch(plist, /<key>UISupportedInterfaceOrientations/u)
    assert.doesNotMatch(project, /UILaunchScreen:/u)
    assert.doesNotMatch(project, /UISupportedInterfaceOrientations/u)
  })

  it('keeps the brand display name when a locale does not override it', () => {
    for (const platform of ['iOS', 'macOS']) {
      for (const locale of ['en', 'es', 'fr', 'pt']) {
        const strings = readRepoFile(
          `swift-clients/apps/${platform}/Resources/${locale}.lproj/InfoPlist.strings`,
        )
        const displayName = strings.match(/"CFBundleDisplayName"\s*=\s*"([^"]*)"/u)
        if (displayName) assert.equal(displayName[1], 'Voucha', `${platform} ${locale}`)
      }
    }
  })
})
