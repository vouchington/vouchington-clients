import { mkdir, mkdtemp, readFile, realpath, rm, symlink, writeFile } from 'node:fs/promises'
import assert from 'node:assert/strict'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'

import { stageNativeContract } from '../scripts/stage-native-contract.mjs'
import { assertExtractedLocalizationRepresentatives } from '../scripts/assert-extracted-localization.mjs'

async function writeTree(root, files) {
  for (const [path, contents] of Object.entries(files)) {
    const file = join(root, path)
    await mkdir(dirname(file), { recursive: true })
    await writeFile(file, contents)
  }
}

async function fixture(t, { legacy = true } = {}) {
  const parent = await mkdtemp(join(tmpdir(), 'voucha-contract-stage-'))
  const filamentsRoot = join(parent, 'filaments')
  const consumerRoot = join(parent, 'candidate-clients')
  const outputRoot = join(parent, 'stage')
  await Promise.all([
    mkdir(consumerRoot, { recursive: true }),
    mkdir(outputRoot, { recursive: true }),
  ])
  await writeTree(filamentsRoot, {
    'api-fixtures/v1/manifest.json': '{"fixtures":[]}\n',
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
  return { consumerRoot, filamentsRoot, outputRoot }
}

test('falls back to legacy localization trees when the Filaments script lacks the isolated exporter interface', async t => {
  const options = await fixture(t)
  await writeTree(options.filamentsRoot, {
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

test('prefers the Filaments-owned exporter over legacy localization trees when available', async t => {
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
      filamentsRoot: options.filamentsRoot,
      outputRoot: options.outputRoot,
    },
  ])
  assert.equal(
    await readFile(join(options.outputRoot, 'api-fixtures/v1/manifest.json'), 'utf8'),
    '{"fixtures":[]}\n',
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

test('runs the Filaments-owned exporter from the producer root', async t => {
  const options = await fixture(t, { legacy: false })
  const realFilamentsRoot = await realpath(options.filamentsRoot)
  await writeTree(options.filamentsRoot, {
    'dev/native-localization.mts': `
      import { mkdir, writeFile } from "node:fs/promises";
      import { join } from "node:path";
      if (process.cwd() !== ${JSON.stringify(realFilamentsRoot)})
        throw new Error("exporter must run from the Filaments root");
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

test('fails closed for partial legacy trees, symlinks, a nonempty output, and invalid exporter output', async t => {
  const missingFixture = await fixture(t)
  await rm(join(missingFixture.filamentsRoot, 'api-fixtures'), { recursive: true })
  await assert.rejects(stageNativeContract(missingFixture), /missing directory.*api-fixtures\/v1/)

  const nestedOutput = await fixture(t)
  await assert.rejects(
    stageNativeContract({ ...nestedOutput, outputRoot: join(nestedOutput.filamentsRoot, 'stage') }),
    /stage output root must be isolated/,
  )

  const candidateOutput = await fixture(t)
  await assert.rejects(
    stageNativeContract({ ...candidateOutput, outputRoot: candidateOutput.consumerRoot }),
    /stage output root must be isolated/,
  )

  const partial = await fixture(t, { legacy: false })
  await mkdir(
    join(partial.filamentsRoot, 'swift-clients/ui/Sources/VouchaLocalization/Generated'),
    { recursive: true },
  )
  await assert.rejects(stageNativeContract(partial), /partial legacy localization directories/)

  const linked = await fixture(t)
  await symlink(
    'UiMessageKey.swift',
    join(linked.filamentsRoot, 'swift-clients/ui/Sources/VouchaLocalization/Generated/alias.swift'),
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
    /Filaments exporter failed: exit 1/,
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
