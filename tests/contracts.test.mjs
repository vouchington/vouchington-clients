import { cp, mkdir, mkdtemp, readFile, rename, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import assert from 'node:assert/strict'
import test from 'node:test'

import { checkContracts, syncContracts, verifyContract } from '../scripts/contracts.mjs'

const paths = [
  'api-fixtures/v1',
  'swift-clients/ui/Sources/VouchaLocalization/Generated',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated',
]

async function writeTree(root, files) {
  for (const [path, contents] of Object.entries(files)) {
    const file = join(root, path)
    await mkdir(dirname(file), { recursive: true })
    await writeFile(file, contents)
  }
}

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), 'voucha-filaments-'))
  await writeTree(root, {
    'api-fixtures/v1/manifest.json': '{"fixtures":[]}\n',
    'swift-clients/ui/Sources/VouchaLocalization/Generated/Resources/en.lproj/Localizable.strings':
      '"example" = "Example";\n',
    'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift':
      'public struct UiMessageKey {}\n',
    'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs':
      'public class UiMessageKey {}\n',
    'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessages.resx': '<root />\n',
  })
  const configRoot = await mkdtemp(join(tmpdir(), 'voucha-contract-config-'))
  const config = join(configRoot, 'filaments.json')
  await writeFile(
    config,
    JSON.stringify({ schemaVersion: 1, repository: 'jonathanong/filaments', ref: 'main', paths }),
  )
  const generatedRoot = await mkdtemp(join(tmpdir(), 'voucha-generated-'))
  t.after(async () => {
    await Promise.all(
      [root, configRoot, generatedRoot].map(path => rm(path, { recursive: true, force: true })),
    )
  })
  const targets = [
    {
      source: 'swift-clients/ui/Sources/VouchaLocalization/Generated',
      destination: join(generatedRoot, 'swift'),
    },
    {
      source: 'dotnet-clients/src/Voucha.Client.Core/Localization/Generated',
      destination: join(generatedRoot, 'dotnet'),
    },
  ]
  for (const target of targets) {
    await mkdir(target.destination, { recursive: true })
    await writeFile(join(target.destination, 'stale.txt'), 'stale\n')
  }
  return { config, root, targets }
}

test('requires an explicit Filaments checkout root', async t => {
  const previous = process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT
  const ancestor = await mkdtemp(join(tmpdir(), 'voucha-contract-ancestor-'))
  t.after(() => rm(ancestor, { recursive: true, force: true }))
  await writeTree(ancestor, {
    'api-fixtures/v1/manifest.json': '{"fixtures":[]}\n',
    'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift':
      'public enum UiMessageKey {}\n',
    'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs':
      'public enum UiMessageKey {}\n',
  })
  const nested = join(ancestor, 'nested', 'client')
  await mkdir(nested, { recursive: true })
  const previousDirectory = process.cwd()
  delete process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT
  try {
    process.chdir(nested)
    await assert.rejects(verifyContract(), /VOUCHA_FILAMENTS_CONTRACT_ROOT is required/)
  } finally {
    process.chdir(previousDirectory)
    if (previous === undefined) delete process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT
    else process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT = previous
  }
})

test('validates the exact declared full-checkout paths', async t => {
  const { config, root } = await fixture(t)
  assert.equal(await verifyContract({ config, root }), root)
  assert.equal(await verifyContract({ config, root: `  ${root}  ` }), root)
  await rm(join(root, 'api-fixtures/v1'), { recursive: true, force: true })
  await assert.rejects(
    verifyContract({ config, root }),
    /declared Filaments contract source api-fixtures\/v1/,
  )
})

test('rejects symlinks within a declared source tree', async t => {
  const { config, root } = await fixture(t)
  await symlink(
    'swift/UiMessageKey.swift',
    join(root, 'swift-clients/ui/Sources/VouchaLocalization/Generated/alias.swift'),
  )
  await assert.rejects(verifyContract({ config, root }), /symlink found/)
})

test('sync replaces both generated localization trees and remains repeatable', async t => {
  const { config, root, targets } = await fixture(t)
  const options = { config, root, targets }
  await assert.rejects(checkContracts(options), /generated localization differs/)
  await syncContracts(options)
  await checkContracts(options)
  await syncContracts(options)
  await checkContracts(options)
})

test('sync recovers an interrupted replacement without Git', async t => {
  const { config, root, targets } = await fixture(t)
  const destination = targets[0].destination
  await rename(destination, `${destination}.contracts-backup`)
  await syncContracts({ config, root, targets })
  await checkContracts({ config, root, targets })
})

test('check catches changed generated localization content', async t => {
  const { config, root, targets } = await fixture(t)
  const options = { config, root, targets }
  await syncContracts(options)
  await writeFile(join(targets[0].destination, 'UiMessageKey.swift'), 'changed\n')
  await assert.rejects(checkContracts(options), /generated localization differs/)
})

test('checks a candidate client checkout without executing it', async t => {
  const { config, root } = await fixture(t)
  const candidate = await mkdtemp(join(tmpdir(), 'voucha-candidate-'))
  t.after(() => rm(candidate, { recursive: true, force: true }))
  const swift = join(candidate, 'swift-clients/ui/Sources/VouchaLocalization/Generated')
  const dotnet = join(
    candidate,
    'dotnet-clients/src/Voucha.Client.Core/Localization/Generated',
  )
  await Promise.all([mkdir(swift, { recursive: true }), mkdir(dotnet, { recursive: true })])
  await Promise.all([
    cp(join(root, paths[1]), swift, { recursive: true }),
    cp(join(root, paths[2]), dotnet, { recursive: true }),
  ])
  await checkContracts({ config, root, destinationRoot: candidate })
  await writeFile(join(swift, 'UiMessageKey.swift'), 'changed\n')
  await assert.rejects(
    checkContracts({ config, root, destinationRoot: candidate }),
    /generated localization differs/,
  )
})

test('does not accept a copied source bundle in place of the checkout layout', async t => {
  const { config, root } = await fixture(t)
  const copied = await mkdtemp(join(tmpdir(), 'voucha-copied-contracts-'))
  t.after(() => rm(copied, { recursive: true, force: true }))
  await cp(
    join(root, 'swift-clients/ui/Sources/VouchaLocalization/Generated'),
    join(copied, 'Generated'),
    { recursive: true },
  )
  await assert.rejects(
    verifyContract({ config, root: copied }),
    /declared Filaments contract source api-fixtures\/v1/,
  )
  assert.equal(
    await readFile(join(root, 'api-fixtures/v1/manifest.json'), 'utf8'),
    '{"fixtures":[]}\n',
  )
})
