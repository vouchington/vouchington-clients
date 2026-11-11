import { mkdir, mkdtemp, readFile, realpath, rm, symlink, writeFile } from 'node:fs/promises'
import assert from 'node:assert/strict'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'

import { stageNativeContract } from '../scripts/stage-native-contract.mjs'
import { assertExtractedLocalizationRepresentatives } from '../scripts/assert-extracted-localization.mjs'
import { writeTree } from './fixture-files.mjs'

async function fixture(t, { legacy = true } = {}) {
  const parent = await mkdtemp(join(tmpdir(), 'voucha-contract-stage-'))
  const vouchingtonRoot = join(parent, 'vouchington')
  const consumerRoot = join(parent, 'candidate-clients')
  const outputRoot = join(parent, 'stage')
  await Promise.all([
    mkdir(consumerRoot, { recursive: true }),
    mkdir(outputRoot, { recursive: true }),
  ])
  await writeTree(vouchingtonRoot, {
    'api-fixtures/v1/manifest.json': '{"fixtures":[]}\n',
    'api-contracts/v1/request-contracts.json': '{"requests":[]}\n',
    ...(legacy
      ? {
          'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift':
            'enum UiMessageKey {}\n',
          'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs':
            'class UiMessageKey {}\n',
        }
      : {}),
  })
  t.after(() => rm(parent, { recursive: true, force: true }))
  return { consumerRoot, vouchingtonRoot, outputRoot }
}

test('falls back to legacy localization trees when the Vouchington script lacks the isolated exporter interface', async t => {
  const options = await fixture(t)
  await writeTree(options.vouchingtonRoot, {
    'dev/native-localization.mts': 'await writeNativeResourceFiles({ root: process.cwd() });\n',
  })
  assert.equal(await stageNativeContract(options), 'legacy')
})

test('stages every declared legacy contract directory into an isolated root', async t => {
  const options = await fixture(t)
  assert.equal(await stageNativeContract(options), 'legacy')
  assert.equal(
    await readFile(join(options.outputRoot, 'api-fixtures/v1/manifest.json'), 'utf8'),
    '{"fixtures":[]}\n',
  )
  assert.equal(
    await readFile(join(options.outputRoot, 'api-contracts/v1/request-contracts.json'), 'utf8'),
    '{"requests":[]}\n',
  )
  assert.equal(
    await readFile(
      join(
        options.outputRoot,
        'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift',
      ),
      'utf8',
    ),
    'enum UiMessageKey {}\n',
  )
  assert.equal(
    await readFile(
      join(
        options.outputRoot,
        'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs',
      ),
      'utf8',
    ),
    'class UiMessageKey {}\n',
  )
})

test('prefers the Vouchington-owned exporter over legacy localization trees when available', async t => {
  const options = await fixture(t)
  const calls = []
  await stageNativeContract({
    ...options,
    runExporter: async arguments_ => {
      calls.push(arguments_)
      await writeTree(options.outputRoot, {
        'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift':
          'exported swift\n',
        'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs':
          'exported dotnet\n',
      })
    },
  })
  assert.deepEqual(calls, [
    {
      consumerRoot: options.consumerRoot,
      vouchingtonRoot: options.vouchingtonRoot,
      outputRoot: options.outputRoot,
    },
  ])
  assert.equal(
    await readFile(join(options.outputRoot, 'api-fixtures/v1/manifest.json'), 'utf8'),
    '{"fixtures":[]}\n',
  )
  assert.equal(
    await readFile(join(options.outputRoot, 'api-contracts/v1/request-contracts.json'), 'utf8'),
    '{"requests":[]}\n',
  )
  assert.equal(
    await readFile(
      join(
        options.outputRoot,
        'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift',
      ),
      'utf8',
    ),
    'exported swift\n',
  )
})

test('runs the Vouchington-owned exporter from the producer root', async t => {
  const options = await fixture(t, { legacy: false })
  const realVouchingtonRoot = await realpath(options.vouchingtonRoot)
  await writeTree(options.vouchingtonRoot, {
    'dev/native-localization.mts': `
      import { mkdir, writeFile } from "node:fs/promises";
      import { join } from "node:path";
      if (process.cwd() !== ${JSON.stringify(realVouchingtonRoot)})
        throw new Error("exporter must run from the Vouchington root");
      const outputRoot = process.argv[process.argv.indexOf("--output-root") + 1];
      await mkdir(join(outputRoot, "swift-clients/ui/Sources/VouchaLocalization/Generated"), { recursive: true });
      await mkdir(join(outputRoot, "dotnet-clients/src/Voucha.Client.Core/Localization/Generated"), { recursive: true });
      await writeFile(join(outputRoot, "swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift"), "exported swift\\n");
      await writeFile(join(outputRoot, "dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs"), "exported dotnet\\n");
      // --output-root --consumer-root
    `,
  })
  assert.equal(await stageNativeContract(options), 'exporter')
})

test('stages an isolated exporter output tree as data without executing producer code', async t => {
  const options = await fixture(t, { legacy: false })
  const exporterOutputRoot = join(options.vouchingtonRoot, '..', 'isolated-exporter-output')
  await mkdir(exporterOutputRoot)
  await mkdir(join(options.vouchingtonRoot, 'dev'), { recursive: true })
  await writeTree(exporterOutputRoot, {
    'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift': 'isolated swift\n',
    'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs':
      'isolated dotnet\n',
  })
  await writeFile(
    join(options.vouchingtonRoot, 'dev/native-localization.mts'),
    'throw new Error("candidate exporter must not run on the trusted host");\n',
  )
  assert.equal(
    await stageNativeContract({ ...options, exporterOutputRoot }),
    'isolated-exporter-data',
  )
  assert.equal(
    await readFile(join(options.outputRoot, 'api-contracts/v1/request-contracts.json'), 'utf8'),
    '{"requests":[]}\n',
  )
  assert.equal(
    await readFile(
      join(
        options.outputRoot,
        'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift',
      ),
      'utf8',
    ),
    'isolated swift\n',
  )

  const unexpected = join(options.vouchingtonRoot, '..', 'isolated-exporter-unexpected')
  await mkdir(unexpected)
  const secondOutputRoot = join(options.vouchingtonRoot, '..', 'stage-two')
  await mkdir(secondOutputRoot)
  await writeTree(unexpected, {
    'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift': 'swift\n',
    'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs': 'dotnet\n',
    'api-fixtures/v1/manifest.json': 'must not replace producer fixtures\n',
  })
  await assert.rejects(
    stageNativeContract({
      ...options,
      outputRoot: secondOutputRoot,
      exporterOutputRoot: unexpected,
    }),
    /unexpected staged contract path api-fixtures/u,
  )
})

test('asserts extracted representatives from the staged client contract', async t => {
  const options = await fixture(t, { legacy: false })
  await stageNativeContract({
    ...options,
    runExporter: async () => {
      await writeTree(options.outputRoot, {
        'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift':
          'nativeSwiftTopHashtagsTopHashtags\n',
        'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs':
          'NativeDotnetTopHashtagsTopHashtags\n',
      })
    },
  })
  await assertExtractedLocalizationRepresentatives(options.outputRoot)
  await writeFile(
    join(
      options.outputRoot,
      'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift',
    ),
    'missing\n',
  )
  await assert.rejects(
    assertExtractedLocalizationRepresentatives(options.outputRoot),
    /Swift extracted/,
  )
})

test('rejects a symbolic link in place of the Vouchington exporter script', async t => {
  const options = await fixture(t)
  await mkdir(join(options.vouchingtonRoot, 'dev'))
  await symlink(
    'native-localization.mts',
    join(options.vouchingtonRoot, 'dev/native-localization.mts'),
  )
  await assert.rejects(
    stageNativeContract(options),
    /Native contract staging failed: symbolic link found at Vouchington exporter dev\/native-localization\.mts/,
  )
})

test('fails closed for partial legacy trees, symlinks, a nonempty output, and invalid exporter output', async t => {
  const missingFixture = await fixture(t)
  await rm(join(missingFixture.vouchingtonRoot, 'api-fixtures'), { recursive: true })
  await assert.rejects(stageNativeContract(missingFixture), /missing directory.*api-fixtures\/v1/)

  const nestedOutput = await fixture(t)
  await assert.rejects(
    stageNativeContract({ ...nestedOutput, outputRoot: join(nestedOutput.vouchingtonRoot, 'stage') }),
    /stage output root must be isolated/,
  )

  const candidateOutput = await fixture(t)
  await assert.rejects(
    stageNativeContract({ ...candidateOutput, outputRoot: candidateOutput.consumerRoot }),
    /stage output root must be isolated/,
  )

  const partial = await fixture(t, { legacy: false })
  await mkdir(
    join(partial.vouchingtonRoot, 'swift-clients/ui/Sources/VouchaLocalization/Generated'),
    { recursive: true },
  )
  await assert.rejects(stageNativeContract(partial), /partial legacy localization directories/)

  const linked = await fixture(t)
  await symlink(
    'UiMessageKey.swift',
    join(linked.vouchingtonRoot, 'swift-clients/ui/Sources/VouchaLocalization/Generated/alias.swift'),
  )
  await assert.rejects(stageNativeContract(linked), /symbolic link/)

  const nonempty = await fixture(t)
  await mkdir(nonempty.outputRoot, { recursive: true })
  await writeFile(join(nonempty.outputRoot, 'leftover'), 'nope\n')
  await assert.rejects(stageNativeContract(nonempty), /stage output root must be empty/)

  const exporter = await fixture(t, { legacy: false })
  await assert.rejects(
    stageNativeContract({ ...exporter, runExporter: async () => {} }),
    /missing directory.*Generated/,
  )

  const failedExporter = await fixture(t, { legacy: false })
  await assert.rejects(
    stageNativeContract({
      ...failedExporter,
      runExporter: async () => {
        throw new Error('exit 1')
      },
    }),
    /Vouchington exporter failed: exit 1/,
  )

  const unexpectedExporterOutput = await fixture(t, { legacy: false })
  await assert.rejects(
    stageNativeContract({
      ...unexpectedExporterOutput,
      runExporter: async () => {
        await writeTree(unexpectedExporterOutput.outputRoot, {
          'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift':
            'exported swift\n',
          'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs':
            'exported dotnet\n',
          'undeclared/extra.txt': 'nope\n',
        })
      },
    }),
    /unexpected staged contract path undeclared/,
  )

  const linkedFixtureAncestor = await fixture(t, { legacy: false })
  const outside = join(dirname(linkedFixtureAncestor.outputRoot), 'outside')
  await mkdir(outside)
  await assert.rejects(
    stageNativeContract({
      ...linkedFixtureAncestor,
      runExporter: async () => {
        await writeTree(linkedFixtureAncestor.outputRoot, {
          'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift':
            'exported swift\n',
          'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs':
            'exported dotnet\n',
        })
        await symlink(outside, join(linkedFixtureAncestor.outputRoot, 'api-fixtures'))
      },
    }),
    /symbolic link found at staged contract api-fixtures/,
  )
  await assert.rejects(readFile(join(outside, 'v1/manifest.json')), { code: 'ENOENT' })
})

for (const mode of ['legacy', 'exporter']) {
  test(`rejects missing and symlinked request contracts in ${mode} staging`, async t => {
    for (const symlinked of [false, true]) {
      const options = await fixture(t)
      const source = join(options.vouchingtonRoot, 'api-contracts/v1')
      await rm(source, { recursive: true })
      if (symlinked) await symlink('../../api-fixtures/v1', source)
      await assert.rejects(
        stageNativeContract({
          ...options,
          ...(mode === 'exporter' ? { runExporter: async () => {} } : {}),
        }),
        symlinked ? /symbolic link/ : /missing directory.*api-contracts\/v1/,
      )
    }
  })
}

test('rejects request contracts supplied by the localization exporter', async t => {
  const options = await fixture(t)
  await assert.rejects(
    stageNativeContract({
      ...options,
      runExporter: async () =>
        writeTree(options.outputRoot, {
          'api-contracts/v1/request-contracts.json': 'injected\n',
        }),
    }),
    /unexpected staged contract path api-contracts/,
  )
})
