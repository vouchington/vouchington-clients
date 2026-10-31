import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtemp, readFile, readdir, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'
import { SWIFT_ANDROID_ARTIFACT_PATHS } from '../scripts/swift-repair-common.mjs'

const cliUrl = new URL('../scripts/repair-swift-cli.mjs', import.meta.url).href
const runner = `
  import { main } from ${JSON.stringify(cliUrl)}
  await main(process.argv.slice(1), {
    async prepareSwiftAndroidRepairFromFiles(input) {
      if (input.metadata.fail) throw new Error('preparation failed')
      return { materializer: 'prepared script\\n', provenance: input.metadata }
    },
  })
`

it('writes the CLI repair artifacts to the selected output directory', async () => {
  const root = await mkdtemp(join(tmpdir(), 'repair-cli-'))
  try {
    const inputPath = join(root, 'input.json')
    const inputOutput = join(root, 'input-output')
    const explicitOutput = join(root, 'explicit-output')
    const provenance = { version: 1, skipVersion: '1.2.3' }
    await writeFile(inputPath, JSON.stringify({ metadata: provenance, outputRoot: inputOutput }))
    for (const [args, outputRoot] of [
      [['--input', inputPath, '--output', explicitOutput], explicitOutput],
      [['--input', inputPath], inputOutput],
    ]) {
      const stdout = execFileSync(
        process.execPath,
        ['--input-type=module', '--eval', runner, '--', ...args],
        {
          cwd: root,
          encoding: 'utf8',
        },
      )
      assert.deepEqual(JSON.parse(stdout), { paths: SWIFT_ANDROID_ARTIFACT_PATHS, provenance })
      assert.equal(
        await readFile(join(outputRoot, SWIFT_ANDROID_ARTIFACT_PATHS[0]), 'utf8'),
        'prepared script\n',
      )
      assert.equal(
        await readFile(join(outputRoot, SWIFT_ANDROID_ARTIFACT_PATHS[1]), 'utf8'),
        `${JSON.stringify(provenance, null, 2)}\n`,
      )
    }
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

it('does not write repair artifacts when preparation fails', async () => {
  const root = await mkdtemp(join(tmpdir(), 'repair-cli-failure-'))
  try {
    const inputPath = join(root, 'input.json')
    await writeFile(inputPath, JSON.stringify({ metadata: { fail: true } }))
    assert.throws(
      () =>
        execFileSync(
          process.execPath,
          ['--input-type=module', '--eval', runner, '--', '--input', inputPath],
          {
            cwd: root,
            stdio: 'pipe',
          },
        ),
      /preparation failed/u,
    )
    assert.deepEqual(await readdir(root), ['input.json'])
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})
