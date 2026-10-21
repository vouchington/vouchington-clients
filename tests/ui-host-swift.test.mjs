import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const script = fileURLToPath(
  new URL('../swift-clients/tooling/verify-ui-host-swift.sh', import.meta.url),
)
const repositoryRoot = fileURLToPath(new URL('..', import.meta.url))

async function executable(path, contents) {
  await writeFile(path, `#!/bin/bash\nset -euo pipefail\n${contents}\n`)
  await chmod(path, 0o755)
}

async function fixture(
  t,
  { xcode = '27.1', swift = '6.4', swiftInsideXcode = true, darwin = true } = {},
) {
  const root = await mkdtemp(join(tmpdir(), 'voucha-ui-host-swift-'))
  t.after(() => rm(root, { recursive: true, force: true }))

  const bin = join(root, 'bin')
  const developerDir = join(root, 'Xcode.app', 'Contents', 'Developer')
  const swiftPath = join(
    swiftInsideXcode ? developerDir : root,
    'Toolchains/XcodeDefault.xctoolchain/usr/bin/swift',
  )
  await mkdir(bin, { recursive: true })
  await mkdir(join(developerDir, 'usr/bin'), { recursive: true })
  await mkdir(dirname(swiftPath), { recursive: true })
  await executable(join(bin, 'uname'), `printf '%s\\n' '${darwin ? 'Darwin' : 'Linux'}'`)
  await executable(
    join(bin, 'xcrun'),
    `[[ "$1" == '--find' && "$2" == 'swift' ]] || exit 2\nprintf '%s\\n' '${swiftPath}'`,
  )
  await executable(
    join(developerDir, 'usr/bin/xcodebuild'),
    `printf 'Xcode ${xcode}\\nBuild version fixture\\n'`,
  )
  await executable(swiftPath, `printf 'Apple Swift version ${swift} (swiftlang-${swift})\\n'`)

  return {
    async run() {
      return new Promise((resolve, reject) => {
        const child = spawn('/bin/bash', [script], {
          env: {
            ...process.env,
            DEVELOPER_DIR: developerDir,
            PATH: `${bin}:${process.env.PATH}`,
          },
        })
        let stdout = ''
        let stderr = ''
        child.stdout.setEncoding('utf8').on('data', chunk => {
          stdout += chunk
        })
        child.stderr.setEncoding('utf8').on('data', chunk => {
          stderr += chunk
        })
        child.on('error', reject)
        child.on('close', code => resolve({ code, stdout, stderr }))
      })
    },
  }
}

test('accepts the Swift compiler bundled with the selected Xcode', async t => {
  const f = await fixture(t)
  const result = await f.run()
  assert.equal(result.code, 0, result.stderr)
  assert.match(result.stdout, /Xcode 27\.1, Swift 6\.4/u)
})

test('rejects a compiler outside the selected Xcode with the mise failure prerequisites', async t => {
  const f = await fixture(t, { swiftInsideXcode: false })
  const result = await f.run()
  assert.equal(result.code, 1)
  assert.match(result.stderr, /outside selected Xcode/u)
  assert.match(result.stderr, /MapUserTrackingMode/u)
  assert.match(result.stderr, /bash swift-clients\/tooling\/test-ui\.sh --filter VouchaUITests/u)
  assert.match(result.stderr, /Xcode 26\.6/u)
  assert.match(result.stderr, /mise shim/u)
})

test('rejects a non-macOS host before looking for Xcode', async t => {
  const f = await fixture(t, { darwin: false })
  const result = await f.run()
  assert.equal(result.code, 1)
  assert.match(result.stderr, /require macOS/u)
})

test('the local UI test entry uses the preflight and the hosted Xcode 26.6 job stays unchanged', async () => {
  const harness = await readFile(join(repositoryRoot, 'swift-clients/tooling/harness.sh'), 'utf8')
  const runner = await readFile(join(repositoryRoot, 'swift-clients/tooling/test-ui.sh'), 'utf8')
  const workflow = await readFile(
    join(repositoryRoot, '.github/workflows/native-contract-tests.yml'),
    'utf8',
  )
  const uiJob = workflow.slice(
    workflow.indexOf('  test-swift-ui:'),
    workflow.indexOf('  swift-patch-coverage:'),
  )

  assert.match(harness, /tooling\/test-ui\.sh/u)
  assert.match(runner, /verify-ui-host-swift\.sh/u)
  assert.match(
    runner,
    /xcrun swift test \\\n  --package-path "\$ROOT_DIR\/swift-clients\/ui" \\\n  --force-resolved-versions/u,
  )
  assert.match(uiJob, /select-xcode\.sh 26\.6/u)
  assert.match(uiJob, /xcrun swift test --package-path swift-clients\/ui/u)
  assert.doesNotMatch(uiJob, /verify-ui-host-swift\.sh/u)
  assert.doesNotMatch(uiJob, /test-ui\.sh/u)
})
