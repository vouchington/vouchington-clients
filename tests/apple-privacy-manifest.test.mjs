import assert from 'node:assert/strict'
import {
  copyFileSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'node:fs'
import { execFileSync, spawnSync } from 'node:child_process'
import { join, relative } from 'node:path'
import { tmpdir } from 'node:os'
import { describe, it } from 'node:test'
import { packageManifests, root, verifier } from './helpers/apple-privacy-fixtures.mjs'
const readRepoFile = path => readFileSync(join(root, path), 'utf8')

const requiredReasonSymbols = [
  { name: 'UserDefaults' },
  { name: 'systemUptime' },
  { name: 'mach_absolute_time' },
  { name: 'activeInputModes' },
  { name: 'contentModificationDateKey' },
  { name: 'creationDateKey' },
  { name: 'modificationDateKey' },
  { name: 'fileModificationDate' },
  { name: 'attributeModificationDateKey' },
  { name: 'NSFileModificationDate' },
  { name: 'NSFileCreationDate' },
  { name: 'NSURLContentModificationDateKey' },
  { name: 'NSURLCreationDateKey' },
  { name: 'volumeAvailableCapacity' },
  { name: 'volumeAvailableCapacityKey' },
  { name: 'volumeAvailableCapacityForImportantUsageKey' },
  { name: 'volumeAvailableCapacityForOpportunisticUsageKey' },
  { name: 'volumeTotalCapacityKey' },
  { name: 'systemFreeSize' },
  { name: 'systemSize' },
  { name: 'getattrlist', call: true },
  { name: 'getattrlistbulk', call: true },
  { name: 'fgetattrlist', call: true },
  { name: 'getattrlistat', call: true },
  { name: 'stat', call: true },
  { name: 'fstat', call: true },
  { name: 'fstatat', call: true },
  { name: 'lstat', call: true },
  { name: 'statfs', call: true },
  { name: 'fstatfs', call: true },
  { name: 'statvfs', call: true },
  { name: 'fstatvfs', call: true },
]

const findRequiredReasonSymbols = source =>
  requiredReasonSymbols
    .filter(({ name, call }) =>
      new RegExp(call ? `\\b${name}\\s*\\(` : `\\b${name}\\b`, 'u').test(source),
    )
    .map(({ name }) => name)

const expectedSymbols = {
  'swift-clients/apps/shared-app/ViewModelFactory.swift': ['UserDefaults'],
  'swift-clients/core/Sources/VouchaCore/NativeBlueskyLinkPersistence.swift': ['UserDefaults'],
  'swift-clients/core/Sources/VouchaCore/NativeBlueskyLinkStore.swift': ['UserDefaults'],
  'swift-clients/core/Sources/VouchaAPI/MemberMCPClient.swift': ['systemUptime'],
  'swift-clients/ui/Sources/VouchaFeatures/NativeParity/NativeCommentThreadViewModel.swift': [
    'UserDefaults',
  ],
}

const packageTarget = (source, name) => {
  const marker = `\n            name: "${name}",`
  const start = source.indexOf(marker)
  assert.notEqual(start, -1, `missing package target ${name}`)
  const from = start + marker.length
  const nextTarget = source.indexOf('\n        .target(', from)
  const nextTestTarget = source.indexOf('\n        .testTarget(', from)
  const ends = [nextTarget, nextTestTarget].filter(index => index !== -1)
  return source.slice(start, ends.length === 0 ? source.length : Math.min(...ends))
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

const makeSourceRoot = directory => {
  const paths = [
    'swift-clients/apps/iOS/PrivacyInfo.xcprivacy',
    'swift-clients/apps/macOS/PrivacyInfo.xcprivacy',
    ...Object.values(packageManifests),
  ]
  for (const path of paths) {
    const destination = join(directory, path)
    mkdirSync(join(destination, '..'), { recursive: true })
    copyFileSync(join(root, path), destination)
  }
}

const mutatePlist = (path, operations) => {
  const script = [
    'import json, plistlib, sys',
    'path = sys.argv[1]',
    'value = plistlib.load(open(path, "rb"))',
    'for operation in json.loads(sys.argv[2]):',
    '    target = value',
    '    for key in operation["path"][:-1]:',
    '        target = target[key]',
    '    key = operation["path"][-1]',
    '    if operation["action"] == "delete":',
    '        del target[key]',
    '    elif operation["action"] == "replace":',
    '        target[key] = operation["value"]',
    '    else:',
    '        target[key] = operation["value"]',
    'with open(path, "wb") as stream:',
    '    plistlib.dump(value, stream, fmt=plistlib.FMT_XML)',
  ].join('\n')
  execFileSync('python3', ['-c', script, path, JSON.stringify(operations)])
}

const checkSourceRoot = sourceRoot =>
  spawnSync('python3', [verifier, '--source-root', sourceRoot], { encoding: 'utf8' })

describe('Apple privacy manifests', () => {
  it('structurally validates source manifests and checks target resource declarations', () => {
    execFileSync('python3', [verifier, '--source-root', root])
    for (const platform of ['iOS', 'macOS']) {
      const plist = readRepoFile(`swift-clients/apps/${platform}/Info.plist`)
      const project = readRepoFile(`swift-clients/apps/${platform}/project.yml`)
      assert.match(plist, /<key>ITSAppUsesNonExemptEncryption<\/key>\s*<false\/>/u)
      assert.match(project, /ITSAppUsesNonExemptEncryption: false/u)
      assert.match(project, /- path: PrivacyInfo\.xcprivacy\n {8}buildPhase: resources/u)
    }
    const corePackage = readRepoFile('swift-clients/core/Package.swift')
    for (const target of ['VouchaCore', 'VouchaAPI'])
      assert.match(packageTarget(corePackage, target), /\.copy\("PrivacyInfo\.xcprivacy"\)/u)
    assert.match(
      packageTarget(readRepoFile('swift-clients/ui/Package.swift'), 'VouchaFeatures'),
      /\.copy\("PrivacyInfo\.xcprivacy"\)/u,
    )
  })

  it('rejects malformed source plist shapes and privacy declarations', () => {
    const directory = mkdtempSync(join(tmpdir(), 'apple-privacy-source-'))
    const sourceRoot = join(directory, 'source')
    const appManifest = join(sourceRoot, 'swift-clients/apps/iOS/PrivacyInfo.xcprivacy')
    const original = readFileSync(join(root, 'swift-clients/apps/iOS/PrivacyInfo.xcprivacy'))
    makeSourceRoot(sourceRoot)
    const cases = [
      [
        'non-dictionary plist',
        () =>
          writeFileSync(appManifest, '<?xml version="1.0"?><plist version="1.0"><array/></plist>'),
      ],
      [
        'tracking enabled',
        () =>
          mutatePlist(appManifest, [
            { action: 'replace', path: ['NSPrivacyTracking'], value: true },
          ]),
      ],
      [
        'missing tracking flag',
        () => mutatePlist(appManifest, [{ action: 'delete', path: ['NSPrivacyTracking'] }]),
      ],
      [
        'tracking domains',
        () =>
          mutatePlist(appManifest, [
            { action: 'replace', path: ['NSPrivacyTrackingDomains'], value: [] },
          ]),
      ],
      [
        'missing required-reason array',
        () => mutatePlist(appManifest, [{ action: 'delete', path: ['NSPrivacyAccessedAPITypes'] }]),
      ],
      [
        'wrong required-reason array type',
        () =>
          mutatePlist(appManifest, [
            { action: 'replace', path: ['NSPrivacyAccessedAPITypes'], value: 'invalid' },
          ]),
      ],
      [
        'non-dictionary required-reason entry',
        () =>
          mutatePlist(appManifest, [
            { action: 'replace', path: ['NSPrivacyAccessedAPITypes', 0], value: 'invalid' },
          ]),
      ],
      [
        'missing required-reason category',
        () =>
          mutatePlist(appManifest, [
            {
              action: 'delete',
              path: ['NSPrivacyAccessedAPITypes', 0, 'NSPrivacyAccessedAPIType'],
            },
          ]),
      ],
      [
        'missing required-reason reasons',
        () =>
          mutatePlist(appManifest, [
            {
              action: 'delete',
              path: ['NSPrivacyAccessedAPITypes', 0, 'NSPrivacyAccessedAPITypeReasons'],
            },
          ]),
      ],
      [
        'non-string reason',
        () =>
          mutatePlist(appManifest, [
            {
              action: 'replace',
              path: ['NSPrivacyAccessedAPITypes', 0, 'NSPrivacyAccessedAPITypeReasons'],
              value: [1],
            },
          ]),
      ],
      [
        'duplicate required-reason category',
        () =>
          mutatePlist(appManifest, [
            {
              action: 'replace',
              path: ['NSPrivacyAccessedAPITypes', 1, 'NSPrivacyAccessedAPIType'],
              value: 'NSPrivacyAccessedAPICategoryUserDefaults',
            },
          ]),
      ],
      [
        'wrong required-reason code',
        () =>
          mutatePlist(appManifest, [
            {
              action: 'replace',
              path: ['NSPrivacyAccessedAPITypes', 0, 'NSPrivacyAccessedAPITypeReasons'],
              value: ['CA92.2'],
            },
          ]),
      ],
      [
        'missing collected-data array',
        () =>
          mutatePlist(appManifest, [{ action: 'delete', path: ['NSPrivacyCollectedDataTypes'] }]),
      ],
      [
        'non-dictionary collected-data entry',
        () =>
          mutatePlist(appManifest, [
            { action: 'replace', path: ['NSPrivacyCollectedDataTypes', 0], value: 'invalid' },
          ]),
      ],
      [
        'missing collected-data type',
        () =>
          mutatePlist(appManifest, [
            {
              action: 'delete',
              path: ['NSPrivacyCollectedDataTypes', 0, 'NSPrivacyCollectedDataType'],
            },
          ]),
      ],
      [
        'duplicate collected-data type',
        () =>
          mutatePlist(appManifest, [
            {
              action: 'replace',
              path: ['NSPrivacyCollectedDataTypes', 1, 'NSPrivacyCollectedDataType'],
              value: 'NSPrivacyCollectedDataTypeEmailAddress',
            },
          ]),
      ],
      [
        'non-array purposes',
        () =>
          mutatePlist(appManifest, [
            {
              action: 'replace',
              path: ['NSPrivacyCollectedDataTypes', 0, 'NSPrivacyCollectedDataTypePurposes'],
              value: 'invalid',
            },
          ]),
      ],
      [
        'non-string purpose',
        () =>
          mutatePlist(appManifest, [
            {
              action: 'replace',
              path: ['NSPrivacyCollectedDataTypes', 0, 'NSPrivacyCollectedDataTypePurposes'],
              value: [1],
            },
          ]),
      ],
      [
        'integer instead of boolean flag',
        () =>
          mutatePlist(appManifest, [
            {
              action: 'replace',
              path: ['NSPrivacyCollectedDataTypes', 0, 'NSPrivacyCollectedDataTypeLinked'],
              value: 1,
            },
          ]),
      ],
      [
        'wrong collection policy',
        () =>
          mutatePlist(appManifest, [
            {
              action: 'replace',
              path: ['NSPrivacyCollectedDataTypes', 0, 'NSPrivacyCollectedDataTypePurposes'],
              value: ['NSPrivacyCollectedDataTypePurposeAdvertising'],
            },
          ]),
      ],
    ]
    try {
      for (const [label, mutate] of cases) {
        writeFileSync(appManifest, original)
        mutate()
        const result = checkSourceRoot(sourceRoot)
        assert.notEqual(result.status, 0, `${label} should be rejected`)
        assert.doesNotMatch(result.stderr, /Traceback/u, `${label} should fail closed cleanly`)
      }
      rmSync(join(sourceRoot, packageManifests.VouchaCore_VouchaCore))
      const missing = checkSourceRoot(sourceRoot)
      assert.notEqual(missing.status, 0)
      assert.doesNotMatch(missing.stderr, /Traceback/u)
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })

  it('fails when shipped Swift uses a required-reason symbol outside the inventory', () => {
    const found = {}
    for (const file of walkSwift(join(root, 'swift-clients'))) {
      const relativePath = relative(root, file)
      const source = readFileSync(file, 'utf8')
      const used = findRequiredReasonSymbols(source)
      if (used.length > 0) found[relativePath] = used
    }
    assert.deepEqual(found, expectedSymbols)
  })

  it('matches C required-reason APIs as calls, not same-named local identifiers', () => {
    assert.deepEqual(findRequiredReasonSymbols('let stat = statistics.rows.count'), [])
    assert.deepEqual(findRequiredReasonSymbols('stat(&fileSystemInfo)'), ['stat'])
  })
})
