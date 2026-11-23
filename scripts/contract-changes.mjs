// Temporary file-level comparison of two staged contract roots. The general folder-to-changes
// comparison is moving into `no-mistakes changes diff --base-root <dir> --head-root <dir>`,
// which replaces this module; delete it (and its use in contracts-diff.mjs) when that lands.
import { createHash } from 'node:crypto'
import { readdir, readFile } from 'node:fs/promises'
import { join, relative, sep } from 'node:path'
import { contractDataPaths, localizationPaths } from './contract-config.mjs'

const fixtureBodies = /^api-fixtures\/v1\/responses\/([^/]+)\.json$/u
const contractsPrefix = 'api-contracts/v1/'

async function hashTree(root) {
  const files = new Map()
  for (const path of [...contractDataPaths, ...localizationPaths]) {
    const entries = await readdir(join(root, path), { recursive: true, withFileTypes: true })
    for (const entry of entries.filter(candidate => candidate.isFile())) {
      const file = join(entry.parentPath, entry.name)
      const digest = createHash('sha256')
        .update(await readFile(file))
        .digest('hex')
      files.set(relative(root, file).split(sep).join('/'), digest)
    }
  }
  return files
}

// A fixture ID is the response body file name under api-fixtures/v1/responses without `.json`;
// the manifest's bodyFile is `responses/<id>.json` for every fixture, and the .NET and Swift
// ApiFixtureLoader read bodyFile by ID.
function categorize(file) {
  const fixture = fixtureBodies.exec(file)
  if (fixture) return ['fixtures', fixture[1]]
  if (localizationPaths.some(path => file.startsWith(`${path}/`))) return ['localization', file]
  return ['contracts', file.startsWith(contractsPrefix) ? file.slice(contractsPrefix.length) : file]
}

export const emptyChanges = () => ({
  fixtures: { changed: [], added: [], removed: [] },
  localization: { changed: [], added: [], removed: [] },
  contracts: { changed: [], added: [], removed: [] },
})

// Manifest entries by fixture ID, so a changed route, request or consumer list reports its ID
// even when the response body is unchanged.
async function manifestEntries(root) {
  const manifest = JSON.parse(await readFile(join(root, 'api-fixtures/v1/manifest.json'), 'utf8'))
  return new Map(manifest.fixtures.map(entry => [entry.id, JSON.stringify(entry)]))
}

export async function compareContractRoots(baseRoot, headRoot) {
  const [base, head] = await Promise.all([hashTree(baseRoot), hashTree(headRoot)])
  const [baseEntries, headEntries] = await Promise.all([
    manifestEntries(baseRoot),
    manifestEntries(headRoot),
  ])
  const changes = emptyChanges()
  for (const [id, entry] of headEntries) {
    if (!baseEntries.has(id)) changes.fixtures.added.push(id)
    else if (baseEntries.get(id) !== entry) changes.fixtures.changed.push(id)
  }
  for (const id of baseEntries.keys()) if (!headEntries.has(id)) changes.fixtures.removed.push(id)
  const record = (file, kind) => {
    const [category, name] = categorize(file)
    changes[category][kind].push(name)
  }
  for (const [file, digest] of head) {
    if (!base.has(file)) record(file, 'added')
    else if (base.get(file) !== digest) record(file, 'changed')
  }
  for (const file of base.keys()) if (!head.has(file)) record(file, 'removed')
  for (const category of Object.values(changes))
    for (const kind of Object.keys(category)) category[kind] = [...new Set(category[kind])].sort()
  return changes
}
