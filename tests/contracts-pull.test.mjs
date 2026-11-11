import { execFileSync } from 'node:child_process'
import { mkdtemp, readdir, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import assert from 'node:assert/strict'
import test from 'node:test'

import { contractDataPaths, localizationPaths } from '../scripts/contract-config.mjs'
import { pinnedCheckout, pullContract } from '../scripts/contracts-pull.mjs'
import { writeTree } from './fixture-files.mjs'

const repositoryRoot = resolve(import.meta.dirname, '..')
const paths = [...contractDataPaths, ...localizationPaths]
const commit = 'a'.repeat(40)
const quiet = () => {}

async function scratch(t, prefix) {
  const directory = await mkdtemp(join(tmpdir(), prefix))
  t.after(() => rm(directory, { recursive: true, force: true }))
  return directory
}

async function fixture(t) {
  const config = join(await scratch(t, 'voucha-pull-config-'), 'vouchington.json')
  await writeFile(
    config,
    JSON.stringify({ schemaVersion: 1, repository: 'vouchington/vouchington', commit, paths }),
  )
  const staged = await scratch(t, 'voucha-pull-staged-')
  await writeTree(staged, {
    'api-fixtures/v1/manifest.json': '{}\n',
    'api-contracts/v1/request-contracts.json': '{}\n',
    [`${localizationPaths[0]}/UiMessageKey.swift`]: 'enum Key {}\n',
    [`${localizationPaths[1]}/UiMessageKey.g.cs`]: 'enum Key {}\n',
  })
  const generatedRoot = await mkdtemp(join(repositoryRoot, '.contracts-test-'))
  t.after(() => rm(generatedRoot, { recursive: true, force: true }))
  const targets = ['swift', 'dotnet'].map((name, index) => ({
    source: localizationPaths[index],
    destination: join(generatedRoot, name),
  }))
  return { config, generatedRoot, staged, targets }
}

test('pull uses an explicit staged root as-is and writes the generated trees', async t => {
  const { config, generatedRoot, staged, targets } = await fixture(t)
  const logs = []
  const root = await pullContract({
    env: { VOUCHA_CONTRACT_ROOT: ` ${staged} ` },
    config,
    log: message => logs.push(message),
    fetchPinned: () => assert.fail('must not fetch when a root is explicit'),
    contractOptions: { targets },
  })
  assert.equal(root, staged)
  assert.match(logs.join('\n'), /as-is/)
  assert.equal(
    await readFile(join(generatedRoot, 'swift/UiMessageKey.swift'), 'utf8'),
    'enum Key {}\n',
  )
  await pullContract({
    env: { VOUCHA_CONTRACT_ROOT: staged },
    config,
    check: true,
    log: quiet,
    contractOptions: { targets, destinationRoot: generatedRoot },
  })
  await writeFile(join(generatedRoot, 'dotnet/UiMessageKey.g.cs'), 'changed\n')
  await assert.rejects(
    pullContract({
      env: { VOUCHA_CONTRACT_ROOT: staged },
      config,
      check: true,
      log: quiet,
      contractOptions: { targets, destinationRoot: generatedRoot },
    }),
    /generated localization differs/,
  )
})

test('pull rejects relative roots, a relative cache and an incomplete explicit root', async t => {
  const { config, staged } = await fixture(t)
  await assert.rejects(
    pullContract({ env: { VOUCHA_CONTRACT_ROOT: 'relative' }, config, log: quiet }),
    /must be an absolute path/,
  )
  await assert.rejects(
    pullContract({ env: { VOUCHA_CONTRACT_CACHE_DIR: 'cache' }, config, log: quiet }),
    /VOUCHA_CONTRACT_CACHE_DIR must be an absolute path/,
  )
  await rm(join(staged, 'api-fixtures/v1'), { recursive: true })
  await assert.rejects(
    pullContract({ env: { VOUCHA_CONTRACT_ROOT: staged }, config, log: quiet }),
    /declared Vouchington contract source api-fixtures\/v1/,
  )
})

test('pull fetches the pinned commit when no root is set and stages through the exporter', async t => {
  const { config, generatedRoot, targets } = await fixture(t)
  const checkout = await scratch(t, 'voucha-pull-checkout-')
  await writeTree(checkout, {
    'api-fixtures/v1/manifest.json': '{}\n',
    'api-contracts/v1/request-contracts.json': '{}\n',
    'dev/native-localization.mts': `import { mkdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
// accepts --output-root and --consumer-root
const output = process.argv[process.argv.indexOf('--output-root') + 1]
for (const [path, name] of [['${localizationPaths[0]}', 'Key.swift'], ['${localizationPaths[1]}', 'Key.g.cs']]) {
  mkdirSync(join(output, path), { recursive: true })
  writeFileSync(join(output, path, name), 'from exporter\\n')
}
`,
  })
  let fetched
  const root = await pullContract({
    env: {},
    config,
    cacheRoot: '/unused',
    log: quiet,
    fetchPinned: (pinned, cacheRoot) => {
      fetched = { commit: pinned.commit, cacheRoot }
      return checkout
    },
    contractOptions: { targets },
  })
  assert.deepEqual(fetched, { commit, cacheRoot: '/unused' })
  assert.equal(root, checkout)
  assert.equal(await readFile(join(generatedRoot, 'swift/Key.swift'), 'utf8'), 'from exporter\n')
})

function localExec(source) {
  return async (command, args, cwd) => {
    if (command === 'git' && args[0] === 'remote') return
    if (command === 'pnpm') return writeFile(join(cwd, 'installed'), 'ok')
    const rewritten =
      args[0] === 'fetch' ? ['fetch', '--quiet', '--depth', '1', source, args.at(-1)] : args
    execFileSync(command, rewritten, { cwd, stdio: 'ignore' })
  }
}

test('pinnedCheckout fetches exactly the commit, installs, caches by SHA and reuses it', async t => {
  const source = await scratch(t, 'voucha-pull-source-')
  const git = (...args) => execFileSync('git', args, { cwd: source, encoding: 'utf8' }).trim()
  git('init', '--quiet')
  await writeFile(join(source, 'file.txt'), 'one\n')
  git('add', '.')
  git('-c', 'user.name=t', '-c', 'user.email=t@example.com', 'commit', '--quiet', '-m', 'one')
  const sha = git('rev-parse', 'HEAD')
  const cacheRoot = await scratch(t, 'voucha-pull-cache-')
  const config = { repository: 'vouchington/vouchington', commit: sha }
  const first = await pinnedCheckout(config, cacheRoot, localExec(source))
  assert.equal(first, join(cacheRoot, sha))
  assert.equal(await readFile(join(first, 'installed'), 'utf8'), 'ok')
  const second = await pinnedCheckout(config, cacheRoot, () => assert.fail('must reuse the cache'))
  assert.equal(second, first)
  await writeFile(join(first, '.voucha-contract-pulled'), 'other\n')
  await assert.rejects(
    pinnedCheckout(config, cacheRoot, quiet),
    /is not the pinned commit .*delete it and retry/,
  )
})

test('pinnedCheckout fails closed when the fetched revision is not the pin', async t => {
  const source = await scratch(t, 'voucha-pull-wrong-')
  execFileSync('git', ['init', '--quiet'], { cwd: source })
  await writeFile(join(source, 'file.txt'), 'x\n')
  execFileSync('git', ['add', '.'], { cwd: source })
  execFileSync(
    'git',
    ['-c', 'user.name=t', '-c', 'user.email=t@example.com', 'commit', '--quiet', '-m', 'x'],
    { cwd: source },
  )
  const cacheRoot = await scratch(t, 'voucha-pull-cache-wrong-')
  const exec = localExec(source)
  const wrongFetch = async (command, args, cwd) =>
    exec(command, args[0] === 'fetch' ? [...args.slice(0, -1), 'HEAD'] : args, cwd)
  await assert.rejects(
    pinnedCheckout({ repository: 'vouchington/vouchington', commit }, cacheRoot, wrongFetch),
    /fetched checkout is not the pinned commit/,
  )
  assert.deepEqual(await readdir(cacheRoot), [])
})
