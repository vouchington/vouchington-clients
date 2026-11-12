import { spawn } from 'node:child_process'
import { lstat, mkdir, mkdtemp, readFile, rename, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { isAbsolute, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { contractConfigPath, readContractConfig } from './contract-config.mjs'
import { checkContracts, syncContracts, verifyContract } from './contracts.mjs'
import { prefixedFailure } from './prefixed-failure.mjs'
import { stageNativeContract } from './stage-native-contract.mjs'

const repositoryRoot = resolve(import.meta.dirname, '..')
const fail = prefixedFailure('Contract pull failed')
const markerName = '.voucha-contract-pulled'
const exporterPath = 'dev/native-localization.mts'

async function exists(path) {
  try {
    await lstat(path)
    return true
  } catch (error) {
    if (error.code === 'ENOENT') return false
    throw error
  }
}

// Subprocess output goes to stderr so stdout carries only the resolved contract root.
function run(command, args, cwd) {
  return new Promise((resolvePromise, reject) => {
    // Windows exposes pnpm as a .cmd shim, which only a shell can launch; its arguments are literals.
    const shell = process.platform === 'win32' && command === 'pnpm'
    const child = spawn(command, args, { cwd, shell, stdio: ['ignore', 2, 2] })
    child.once('error', reject)
    child.once('exit', (code, signal) =>
      code === 0
        ? resolvePromise()
        : reject(new Error(`${command} ${args[0]} failed (${signal ?? `exit ${code}`})`)),
    )
  })
}

async function checkoutHead(path) {
  let output = ''
  await new Promise((resolvePromise, reject) => {
    const child = spawn('git', ['-C', path, 'rev-parse', 'HEAD'], {
      stdio: ['ignore', 'pipe', 'inherit'],
    })
    child.stdout.on('data', chunk => (output += chunk))
    child.once('error', reject)
    // 'close' (not 'exit') guarantees stdout has been fully read before HEAD is compared.
    child.once('close', code => (code === 0 ? resolvePromise() : reject(new Error('rev-parse'))))
  })
  return output.trim()
}

async function cachedCheckout(final, commit) {
  const marker = await readFile(join(final, markerName), 'utf8').catch(() => '')
  if (marker.trim() !== commit || (await checkoutHead(final).catch(() => '')) !== commit)
    fail(`cached checkout ${final} is not the pinned commit ${commit}; delete it and retry`)
  return final
}

/** Shallow-fetches exactly the pinned commit into a SHA-keyed cache and installs exporter deps. */
export async function pinnedCheckout(config, cacheRoot, exec = run) {
  const { commit, repository } = config
  const final = join(cacheRoot, commit)
  if (await exists(final)) return cachedCheckout(final, commit)
  await mkdir(cacheRoot, { recursive: true })
  const partial = await mkdtemp(join(cacheRoot, `${commit}.partial-`))
  try {
    await exec('git', ['init', '--quiet'], partial)
    await exec('git', ['remote', 'add', 'origin', `https://github.com/${repository}.git`], partial)
    await exec('git', ['fetch', '--quiet', '--depth', '1', 'origin', commit], partial)
    await exec('git', ['checkout', '--quiet', '--detach', 'FETCH_HEAD'], partial)
    if ((await checkoutHead(partial)) !== commit)
      fail(`fetched checkout is not the pinned commit ${commit}`)
    await exec('pnpm', ['install', '--frozen-lockfile', '--ignore-scripts'], partial)
    await writeFile(join(partial, markerName), `${commit}\n`)
    try {
      await rename(partial, final)
    } catch (error) {
      // A concurrent pull of the same pin published first; validate and reuse its checkout.
      if (!['EEXIST', 'ENOTEMPTY', 'EPERM'].includes(error.code) || !(await exists(final)))
        throw error
      return cachedCheckout(final, commit)
    }
  } finally {
    await rm(partial, { recursive: true, force: true })
  }
  return final
}

/**
 * Resolves the contract root (an explicit VOUCHA_CONTRACT_ROOT, else the pinned commit), makes
 * its generated native localization available in this checkout, and returns the root. A root
 * with the producer exporter is a full checkout and is staged through it; any other root must
 * already be a staged contract root. With `check`, the repository is only compared, never written.
 */
export async function pullContract({
  env = process.env,
  config = contractConfigPath,
  cacheRoot = env.VOUCHA_CONTRACT_CACHE_DIR?.trim() || join(repositoryRoot, '.cache/vouchington'),
  check = false,
  contractOptions = {},
  log = message => process.stderr.write(`${message}\n`),
  fetchPinned = pinnedCheckout,
} = {}) {
  if (!isAbsolute(cacheRoot)) fail('VOUCHA_CONTRACT_CACHE_DIR must be an absolute path')
  const pinned = await readContractConfig(config)
  const explicit = env.VOUCHA_CONTRACT_ROOT?.trim()
  if (explicit !== undefined && explicit !== '' && !isAbsolute(explicit))
    fail('VOUCHA_CONTRACT_ROOT must be an absolute path')
  let root
  if (explicit) {
    root = resolve(explicit)
    log(`Using VOUCHA_CONTRACT_ROOT ${root} as-is (the pin ${pinned.commit} is not fetched)`)
  } else {
    root = await fetchPinned(pinned, cacheRoot)
    log(`Using ${pinned.repository}@${pinned.commit} at ${root}`)
  }
  let source = root
  let stage
  try {
    if (await exists(join(root, exporterPath))) {
      stage = await mkdtemp(join(tmpdir(), 'voucha-contract-stage-'))
      await stageNativeContract({
        vouchingtonRoot: root,
        consumerRoot: repositoryRoot,
        outputRoot: stage,
      })
      source = stage
    }
    await verifyContract({ config, root: source })
    if (check) await checkContracts({ config, root: source, ...contractOptions })
    else await syncContracts({ config, root: source, ...contractOptions })
  } finally {
    if (stage) await rm(stage, { recursive: true, force: true })
  }
  return root
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const flags = process.argv.slice(2)
  if (flags.length > 1 || (flags.length === 1 && flags[0] !== '--check'))
    throw new Error('Usage: node scripts/contracts-pull.mjs [--check]')
  process.stdout.write(`${await pullContract({ check: flags.length === 1 })}\n`)
}
