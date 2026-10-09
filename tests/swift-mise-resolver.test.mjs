import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmod, mkdir, mkdtemp, realpath, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const resolver = fileURLToPath(
  new URL('../swift-clients/tooling/resolve-mise-swift.sh', import.meta.url),
)
const harness = fileURLToPath(new URL('../swift-clients/tooling/harness.sh', import.meta.url))
const periphery = fileURLToPath(
  new URL('../swift-clients/tooling/periphery-scan.sh', import.meta.url),
)

test('selects only an installed Swift binary inside the active mise root', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-mise-swift-resolver-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const bin = join(root, 'bin')
  const toolchain = join(root, 'installs/swift/6.4.0')
  const swift = join(toolchain, 'usr/bin/swift')
  const systemSwift = join(bin, 'swift')
  const mise = join(bin, 'mise')
  await mkdir(bin, { recursive: true })
  await mkdir(dirname(swift), { recursive: true })
  await writeFile(swift, "#!/bin/sh\nprintf 'Apple Swift version 6.4 (swiftlang-6.4)\\n'\n")
  await chmod(swift, 0o755)
  await writeFile(systemSwift, '#!/bin/sh\nexit 0\n')
  await chmod(systemSwift, 0o755)

  async function run(selectedRoot, selectedSwift) {
    await writeFile(
      mise,
      `#!/bin/bash\nif [[ "$1" == where && "$2" == swift ]]; then printf '%s\\n' '${selectedRoot}'; exit 0; fi\nif [[ "$1" == which && "$2" == swift ]]; then printf '%s\\n' '${selectedSwift}'; exit 0; fi\nexit 2\n`,
    )
    await chmod(mise, 0o755)
    return spawnSync('bash', [resolver], {
      encoding: 'utf8',
      env: { ...process.env, PATH: `${bin}:${process.env.PATH}` },
    })
  }

  const installed = await run(toolchain, swift)
  assert.equal(installed.status, 0, installed.stderr)
  assert.equal(installed.stdout.trim(), await realpath(swift))

  const missing = await run(join(root, 'installs/swift/missing'), systemSwift)
  assert.notEqual(missing.status, 0)
  assert.match(missing.stderr, /not installed/u)
  for (const [script, args] of [
    [harness, ['--checks', 'build']],
    [periphery, ['core']],
  ]) {
    const result = spawnSync('bash', [script, ...args], {
      encoding: 'utf8',
      env: { ...process.env, PATH: `${bin}:${process.env.PATH}` },
    })
    assert.notEqual(result.status, 0, `${script} must reject a missing pin`)
    assert.match(result.stderr, /not installed/u)
  }

  const outside = await run(toolchain, systemSwift)
  assert.notEqual(outside.status, 0)
  assert.match(outside.stderr, /outside its pinned installation root/u)

  await writeFile(swift, "#!/bin/sh\nprintf 'Swift version 6.3.3 (swiftlang-6.3.3)\\n'\n")
  const wrongVersion = await run(toolchain, swift)
  assert.notEqual(wrongVersion.status, 0)
  assert.match(wrongVersion.stderr, /requires Swift 6\.4\.0/u)
})
