import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { join, relative } from 'node:path'
import { describe, it } from 'node:test'

const repoRoot = new URL('..', import.meta.url)
const readRepoFile = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8')

const symbols = [
  'UserDefaults',
  'systemUptime',
  'mach_absolute_time',
  'volumeAvailableCapacity',
  'activeInputModes',
  'contentModificationDateKey',
  'creationDateKey',
  'attributeModificationDateKey',
  'NSFileModificationDate',
  'NSFileCreationDate',
  'NSURLContentModificationDateKey',
  'NSURLCreationDateKey',
  'statfs',
  'statvfs',
  'fstatfs',
  'getattrlist',
]

const expectedSymbols = {
  'swift-clients/apps/shared-app/ViewModelFactory.swift': ['UserDefaults'],
  'swift-clients/core/Sources/VouchaCore/NativeBlueskyLinkPersistence.swift': ['UserDefaults'],
  'swift-clients/core/Sources/VouchaCore/NativeBlueskyLinkStore.swift': ['UserDefaults'],
  'swift-clients/core/Sources/VouchaAPI/MemberMCPClient.swift': ['systemUptime'],
}

const collectedTypes = [
  'NSPrivacyCollectedDataTypeEmailAddress',
  'NSPrivacyCollectedDataTypeUserID',
  'NSPrivacyCollectedDataTypeDeviceID',
  'NSPrivacyCollectedDataTypePhotosorVideos',
  'NSPrivacyCollectedDataTypeOtherUserContent',
]

const plistStrings = (xml, key) => {
  const match = xml.match(
    new RegExp(`<key>${key}</key>\\s*(<array>[\\s\\S]*?</array>|<string>[\\s\\S]*?</string>)`, 'u'),
  )
  assert.ok(match, `missing ${key}`)
  return [...match[1].matchAll(/<string>([^<]*)<\/string>/gu)].map(item => item[1])
}

const reasonMap = xml => {
  const blocks = xml.match(/<dict>\s*<key>NSPrivacyAccessedAPIType<\/key>[\s\S]*?<\/dict>/gu) ?? []
  return Object.fromEntries(
    blocks.map(block => {
      const category = block.match(
        /<key>NSPrivacyAccessedAPIType<\/key>\s*<string>([^<]*)<\/string>/u,
      )?.[1]
      assert.ok(category)
      return [category, plistStrings(block, 'NSPrivacyAccessedAPITypeReasons')]
    }),
  )
}

const walkSwift = directory => {
  const files = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (
      entry.name.startsWith('.') ||
      ['Tests', 'test-support', 'checkouts', 'node_modules'].includes(entry.name)
    ) {
      continue
    }
    const path = join(directory, entry.name)
    if (entry.isDirectory()) files.push(...walkSwift(path))
    else if (entry.name.endsWith('.swift')) files.push(path)
  }
  return files
}

describe('Apple privacy manifests', () => {
  it('declares only the audited required-reason APIs and collected types', () => {
    const appReasons = {
      NSPrivacyAccessedAPICategoryUserDefaults: ['CA92.1'],
      NSPrivacyAccessedAPICategorySystemBootTime: ['35F9.1'],
    }
    for (const platform of ['iOS', 'macOS']) {
      const plist = readRepoFile(`swift-clients/apps/${platform}/Info.plist`)
      const project = readRepoFile(`swift-clients/apps/${platform}/project.yml`)
      const privacy = readRepoFile(`swift-clients/apps/${platform}/PrivacyInfo.xcprivacy`)
      assert.match(plist, /<key>ITSAppUsesNonExemptEncryption<\/key>\s*<false\/>/u)
      assert.match(project, /ITSAppUsesNonExemptEncryption: false/u)
      assert.match(project, /- path: PrivacyInfo\.xcprivacy\n {8}buildPhase: resources/u)
      assert.match(privacy, /<key>NSPrivacyTracking<\/key>\s*<false\/>/u)
      assert.match(privacy, /<key>NSPrivacyTrackingDomains<\/key>\s*<array\/>/u)
      assert.deepEqual(reasonMap(privacy), appReasons)
      assert.deepEqual(
        [
          ...privacy.matchAll(
            /<key>NSPrivacyCollectedDataType<\/key>\s*<string>([^<]*)<\/string>/gu,
          ),
        ].map(match => match[1]),
        collectedTypes,
      )
    }

    const core = readRepoFile('swift-clients/core/Sources/VouchaCore/PrivacyInfo.xcprivacy')
    const api = readRepoFile('swift-clients/core/Sources/VouchaAPI/PrivacyInfo.xcprivacy')
    const packageSwift = readRepoFile('swift-clients/core/Package.swift')
    assert.deepEqual(reasonMap(core), { NSPrivacyAccessedAPICategoryUserDefaults: ['CA92.1'] })
    assert.deepEqual(reasonMap(api), { NSPrivacyAccessedAPICategorySystemBootTime: ['35F9.1'] })
    assert.match(core, /<key>NSPrivacyCollectedDataTypes<\/key>\s*<array\/>/u)
    assert.match(api, /<key>NSPrivacyCollectedDataTypes<\/key>\s*<array\/>/u)
    assert.match(packageSwift, /name: "VouchaCore"[\s\S]*\.copy\("PrivacyInfo\.xcprivacy"\)/u)
    assert.match(packageSwift, /name: "VouchaAPI"[\s\S]*\.copy\("PrivacyInfo\.xcprivacy"\)/u)
  })

  it('fails when shipped Swift uses a required-reason symbol outside the inventory', () => {
    const root = repoRoot.pathname
    const found = {}
    for (const file of walkSwift(join(root, 'swift-clients'))) {
      const relativePath = relative(root, file)
      const source = readFileSync(file, 'utf8')
      const used = symbols.filter(symbol => new RegExp(`\\b${symbol}\\b`, 'u').test(source))
      if (used.length > 0) found[relativePath] = used
    }
    assert.deepEqual(found, expectedSymbols)

    const inventory = readRepoFile('docs/development/apple-privacy-inventory.md')
    for (const path of Object.keys(expectedSymbols))
      assert.match(inventory, new RegExp(path.replaceAll('.', '\\.'), 'u'))
    assert.match(inventory, /CA92\.1/u)
    assert.match(inventory, /35F9\.1/u)
    assert.match(inventory, /## Open decisions/u)
  })
})
