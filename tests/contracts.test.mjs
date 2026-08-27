import { cp, mkdir, mkdtemp, readFile, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import assert from 'node:assert/strict'
import test from 'node:test'

import { checkContracts, syncContracts, verifyContract } from '../scripts/contracts.mjs'

const paths = [
  { source: 'api-fixtures/v1', destination: 'api-fixtures/v1' },
  { source: 'client-contracts/v1/native-localization', destination: 'native-localization' },
]

async function writeTree(root, files) {
  for (const [path, contents] of Object.entries(files)) {
    const file = join(root, path)
    await mkdir(dirname(file), { recursive: true })
    await writeFile(file, contents)
  }
}

async function fixture() {
  const root = await mkdtemp(join(tmpdir(), 'voucha-filaments-'))
  await writeTree(root, {
    'api-fixtures/v1/manifest.json': '{"fixtures":[]}\n',
    'client-contracts/v1/native-localization/swift/Resources/en.lproj/Localizable.strings':
      '"example" = "Example";\n',
    'client-contracts/v1/native-localization/swift/UiMessageKey.swift':
      'public struct UiMessageKey {}\n',
    'client-contracts/v1/native-localization/dotnet/UiMessageKey.g.cs':
      'public class UiMessageKey {}\n',
    'client-contracts/v1/native-localization/dotnet/UiMessages.resx': '<root />\n',
  })
  const config = join(await mkdtemp(join(tmpdir(), 'voucha-contract-config-')), 'filaments.json')
  await writeFile(
    config,
    JSON.stringify({ schemaVersion: 1, repository: 'jonathanong/filaments', ref: 'main', paths }),
  )
  const generatedRoot = await mkdtemp(join(tmpdir(), 'voucha-generated-'))
  const targets = [
    {
      source: 'client-contracts/v1/native-localization/swift',
      destination: join(generatedRoot, 'swift'),
    },
    {
      source: 'client-contracts/v1/native-localization/dotnet',
      destination: join(generatedRoot, 'dotnet'),
    },
  ]
  for (const target of targets) {
    await mkdir(target.destination, { recursive: true })
    await writeFile(join(target.destination, 'stale.txt'), 'stale\n')
  }
  return { config, root, targets }
}

test('requires an explicit Filaments checkout root', async () => {
  const previous = process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT
  delete process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT
  try {
    await assert.rejects(verifyContract(), /VOUCHA_FILAMENTS_CONTRACT_ROOT is required/)
  } finally {
    if (previous === undefined) delete process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT
    else process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT = previous
  }
})

test('validates the exact declared full-checkout paths', async () => {
  const { config, root } = await fixture()
  assert.equal(await verifyContract({ config, root }), root)
  await rm(join(root, 'api-fixtures/v1'), { recursive: true, force: true })
  await assert.rejects(
    verifyContract({ config, root }),
    /declared Filaments contract source api-fixtures\/v1/,
  )
})

test('rejects symlinks within a declared source tree', async () => {
  const { config, root } = await fixture()
  await symlink(
    'swift/UiMessageKey.swift',
    join(root, 'client-contracts/v1/native-localization/alias.swift'),
  )
  await assert.rejects(verifyContract({ config, root }), /symlink found/)
})

test('sync replaces both generated localization trees and remains repeatable', async () => {
  const { config, root, targets } = await fixture()
  const options = { config, root, targets }
  await assert.rejects(checkContracts(options), /generated localization differs/)
  await syncContracts(options)
  await checkContracts(options)
  await syncContracts(options)
  await checkContracts(options)
})

test('check catches changed generated localization content', async () => {
  const { config, root, targets } = await fixture()
  const options = { config, root, targets }
  await syncContracts(options)
  await writeFile(join(targets[0].destination, 'UiMessageKey.swift'), 'changed\n')
  await assert.rejects(checkContracts(options), /generated localization differs/)
})

test('does not accept a copied source bundle in place of the checkout layout', async () => {
  const { config, root } = await fixture()
  const copied = await mkdtemp(join(tmpdir(), 'voucha-copied-contracts-'))
  await cp(
    join(root, 'client-contracts/v1/native-localization'),
    join(copied, 'native-localization'),
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
