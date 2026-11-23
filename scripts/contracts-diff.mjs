import { execFileSync } from 'node:child_process'
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { compareContractRoots, emptyChanges } from './contract-changes.mjs'
import { readContractConfig } from './contract-config.mjs'
import { defaultCacheRoot, pinnedCheckout } from './contracts-pull.mjs'
import { stageNativeContract } from './stage-native-contract.mjs'

const repositoryRoot = resolve(import.meta.dirname, '..')
const usage = 'Usage: node scripts/contracts-diff.mjs --base <rev> --head <rev> [--changes <file>]'

function parseArguments(argv) {
  const options = {}
  for (let index = 0; index < argv.length; index += 2) {
    const name = argv[index]?.slice(2)
    const value = argv[index + 1]
    if (!['base', 'head', 'changes'].includes(name) || !value || options[name] !== undefined)
      throw new Error(usage)
    options[name] = value
  }
  if (!options.base || !options.head) throw new Error(usage)
  return options
}

/** Reads and validates contracts/vouchington.json as it was at a git revision of this repo. */
async function pinAt(revision, gitRoot) {
  const directory = await mkdtemp(join(tmpdir(), 'voucha-contract-pin-'))
  try {
    if (revision.startsWith('-')) throw new Error('revision must not start with a dash')
    const text = execFileSync('git', ['show', `${revision}:contracts/vouchington.json`], {
      cwd: gitRoot,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
    })
    const file = join(directory, 'vouchington.json')
    await writeFile(file, text)
    return await readContractConfig(file)
  } catch (error) {
    throw new Error(`no valid contract pin at ${revision}: ${error.message}`, { cause: error })
  } finally {
    await rm(directory, { recursive: true, force: true })
  }
}

// The producer exporter validates the producer's native consumer manifest against this repo's
// product sources, so each producer commit is staged against the client tree of its own revision.
async function exportRevision(revision, gitRoot, directory) {
  const archive = join(directory, 'revision.tar')
  const options = { cwd: gitRoot, stdio: ['ignore', 'ignore', 'pipe'] }
  execFileSync('git', ['archive', '--format=tar', '--output', archive, revision], options)
  const consumerRoot = join(directory, 'consumer')
  await mkdir(consumerRoot)
  execFileSync('tar', ['-xf', archive, '-C', consumerRoot], options)
  // The exporter lists consumer sources with `git ls-files`, so the extracted tree must be a repo.
  for (const args of [
    ['init', '--quiet'],
    ['add', '--all'],
  ])
    execFileSync('git', args, { ...options, cwd: consumerRoot })
  return consumerRoot
}

// Stages each producer checkout through the same exporter as contracts:pull (generated
// localization included), then compares the two staged roots.
async function stagedChanges(report, gitRoot) {
  const stage = await mkdtemp(join(tmpdir(), 'voucha-contract-diff-'))
  try {
    const roots = []
    for (const [name, side] of [
      ['base', report.base],
      ['head', report.head],
    ]) {
      const directory = join(stage, name)
      await mkdir(directory)
      const consumerRoot = await exportRevision(side.revision, gitRoot, directory)
      const outputRoot = join(directory, 'staged')
      await mkdir(outputRoot)
      await stageNativeContract({ vouchingtonRoot: side.root, consumerRoot, outputRoot })
      roots.push(outputRoot)
    }
    return await compareContractRoots(...roots)
  } finally {
    await rm(stage, { recursive: true, force: true })
  }
}

/**
 * Fetches the producer commits pinned at two revisions of this repo through the same SHA-keyed
 * cache as contracts:pull and reports them. VOUCHA_CONTRACT_ROOT is ignored: a diff needs two
 * roots and both come from pins in this repo's history. Never throws; a failure yields
 * `complete: false`, an `error`, and empty change lists.
 */
export async function diffContracts({
  base,
  head,
  withChanges = false,
  gitRoot = repositoryRoot,
  cacheRoot = defaultCacheRoot(),
  fetchPinned = pinnedCheckout,
}) {
  const report = {
    schemaVersion: 1,
    base: { revision: base, commit: null, root: null },
    head: { revision: head, commit: null, root: null },
    complete: false,
  }
  let changes = emptyChanges()
  try {
    for (const side of [report.base, report.head]) {
      const pin = await pinAt(side.revision, gitRoot)
      side.commit = pin.commit
      side.root = await fetchPinned(pin, cacheRoot)
    }
    if (withChanges && report.base.commit !== report.head.commit)
      changes = await stagedChanges(report, gitRoot)
    report.complete = true
  } catch (error) {
    report.error = error.message
    changes = emptyChanges()
  }
  const side = ({ revision, commit }) => ({ revision, commit })
  return {
    report,
    changes: {
      schemaVersion: 1,
      base: side(report.base),
      head: side(report.head),
      ...changes,
      complete: report.complete,
      ...(report.error === undefined ? {} : { error: report.error }),
    },
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const options = parseArguments(process.argv.slice(2))
  const { report, changes } = await diffContracts({
    base: options.base,
    head: options.head,
    withChanges: options.changes !== undefined,
  })
  if (options.changes) {
    await writeFile(options.changes, `${JSON.stringify(changes, null, 2)}\n`)
    report.changes = resolve(options.changes)
  }
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`)
  if (!report.complete) process.exitCode = 1
}
