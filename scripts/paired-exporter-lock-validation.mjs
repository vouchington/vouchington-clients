import { pairedExporterPackages } from './paired-exporter-runtime-evidence.mjs'

export const packagePins = pairedExporterPackages.map(pin => ({
  ...pin,
  snapshotKey:
    pin.name === '@vouchington/localization'
      ? '@vouchington/localization@0.2.1(react@19.3.1)'
      : undefined,
}))
const expectedDependencies = {
  '@nodable/entities': {},
  'fast-xml-builder': { 'path-expression-matcher': '1.6.2', 'xml-naming': '0.3.0' },
  'fast-xml-parser': {
    '@nodable/entities': '3.1.0',
    'fast-xml-builder': '1.3.1',
    'is-unsafe': '2.0.2',
    'path-expression-matcher': '1.6.2',
    strnum: '2.4.2',
    'xml-naming': '0.3.0',
  },
  'is-unsafe': {},
  'path-expression-matcher': {},
  strnum: { anynum: '1.0.1' },
  'xml-naming': {},
  anynum: {},
  '@vouchington/localization': {},
  '@vouchington/utils': {},
}

function fail(message) {
  throw new Error(`Paired exporter dependency bundle rejected: ${message}`)
}

function parsePackageRecords(lock) {
  const start = lock.indexOf('\npackages:\n')
  const end = lock.indexOf('\nsnapshots:\n', start + 1)
  if (start < 0 || end < 0) fail('pnpm lockfile sections are not in the reviewed format')
  const packages = new Map()
  const block = lock.slice(start + '\npackages:\n'.length, end)
  const lines = block.split('\n')
  let active
  for (const line of lines) {
    const entry = /^  (?:'([^']+)'|([^\s:]+)):\s*(?:\{\})?$/u.exec(line)
    if (entry) {
      active = entry[1] ?? entry[2]
      if (packages.has(active)) fail(`duplicate package lock entry ${active}`)
      packages.set(active, { integrity: undefined })
      continue
    }
    const integrity = /^    resolution: \{integrity: (sha512-[A-Za-z0-9+/=]+)\}$/u.exec(line)
    if (integrity && active) packages.get(active).integrity = integrity[1]
  }
  return packages
}

function parseSnapshotDependencies(lock) {
  const start = lock.indexOf('\nsnapshots:\n')
  if (start < 0) fail('pnpm lockfile has no snapshots section')
  const block = lock.slice(start + '\nsnapshots:\n'.length)
  const lines = block.split('\n')
  const result = new Map()
  let active
  let inDependencies = false
  for (const line of lines) {
    const entry = /^  (?:'([^']+)'|([^\s:]+)):\s*(?:\{\})?$/u.exec(line)
    if (entry) {
      active = entry[1] ?? entry[2]
      result.set(active, {})
      inDependencies = false
      continue
    }
    if (line.startsWith('    dependencies:') && active) {
      inDependencies = true
      continue
    }
    if (/^    [A-Za-z]/u.test(line) && !line.startsWith('    dependencies:')) inDependencies = false
    if (inDependencies) {
      const dependency = /^      (?:'([^']+)'|([^\s:]+)): ([^\s]+)$/u.exec(line)
      if (dependency) result.get(active)[dependency[1] ?? dependency[2]] = dependency[3]
    }
  }
  return result
}

export function verifyPairedExporterLock(lock, packageJson) {
  const declaredDependencyNames = Object.keys(packageJson.dependencies ?? {}).sort()
  const reviewedDependencyNames = [
    '@ts-shared/utils',
    '@vouchington/localization',
    '@vouchington/utils',
    'fast-xml-parser',
  ].sort()
  if (
    JSON.stringify(declaredDependencyNames) !== JSON.stringify(reviewedDependencyNames) ||
    packageJson.dependencies?.['fast-xml-parser'] !== '^5.11.2' ||
    packageJson.dependencies?.['@vouchington/localization'] !== '^0.2.1' ||
    packageJson.dependencies?.['@vouchington/utils'] !== '^0.7.4' ||
    packageJson.dependencies?.['@ts-shared/utils'] !== 'workspace:^'
  )
    fail('the reviewed exporter dependency declaration changed')
  const records = parsePackageRecords(lock)
  const snapshots = parseSnapshotDependencies(lock)
  const importerStart = lock.indexOf('\n  ts-shared/ui-messages:\n')
  const nextImporter =
    importerStart < 0 ? undefined : /\n  [^\s]/u.exec(lock.slice(importerStart + 1))
  const importerEnd = nextImporter ? importerStart + 1 + nextImporter.index : lock.length
  if (importerStart < 0 || importerEnd === lock.length) fail('ui-messages lock importer is missing')
  const importer = lock.slice(importerStart, importerEnd)
  if ((importer.match(/^        version: /gmu) ?? []).length !== reviewedDependencyNames.length)
    fail('ui-messages importer graph changed')
  for (const line of [
    "      '@ts-shared/utils':\n        specifier: workspace:^\n        version: link:../utils",
    "      '@vouchington/localization':\n        specifier: ^0.2.1\n        version: 0.2.1(react@19.3.1)",
    "      '@vouchington/utils':\n        specifier: ^0.7.4\n        version: 0.7.4",
    '      fast-xml-parser:\n        specifier: ^5.11.2\n        version: 5.11.2',
  ])
    if (!importer.includes(line)) fail('ui-messages importer graph changed')
  for (const pin of packagePins) {
    const entry = `${pin.name}@${pin.version}`
    const record = records.get(entry)
    if (!record || record.integrity !== pin.integrity) fail(`reviewed lock entry changed: ${entry}`)
    const dependencies = snapshots.get(pin.snapshotKey ?? entry)
    if (
      !dependencies ||
      JSON.stringify(Object.entries(dependencies).sort()) !==
        JSON.stringify(Object.entries(expectedDependencies[pin.name]).sort())
    )
      fail(`reviewed dependency graph changed: ${entry}`)
  }
  const parserDependencies = snapshots.get('fast-xml-parser@5.11.2')
  if (
    Object.keys(parserDependencies).length !==
    Object.keys(expectedDependencies['fast-xml-parser']).length
  )
    fail('fast-xml-parser dependency closure changed')
}
