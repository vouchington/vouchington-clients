import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const script = fileURLToPath(
  new URL('../swift-clients/apps/android/tooling/verify-android-host-swift.sh', import.meta.url),
)
const workflowPath = fileURLToPath(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
)

async function executable(path, contents) {
  await writeFile(path, `#!/bin/bash\nset -euo pipefail\n${contents}\n`)
  await chmod(path, 0o755)
}

async function fixture(t, { xcode = '26.6', swift = '6.3.3', swiftInsideXcode = true } = {}) {
  const root = await mkdtemp(join(tmpdir(), 'voucha-android-host-swift-'))
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
  await executable(join(bin, 'uname'), "printf 'Darwin\\n'")
  await executable(
    join(bin, 'xcrun'),
    `[[ "$1" == '--find' && "$2" == 'swift' ]] || exit 2\nprintf '%s\\n' '${swiftPath}'`,
  )
  await executable(
    join(developerDir, 'usr/bin/xcodebuild'),
    `printf 'Xcode ${xcode}\\nBuild version test\\n'`,
  )
  await executable(swiftPath, `printf 'Swift version ${swift} (swift-${swift}-RELEASE)\\n'`)
  await executable(
    join(bin, 'mise'),
    `if [[ "$1" == install && "$2" == 'swift@6.3.3' ]]; then exit 0; fi\nif [[ "$*" == 'exec swift@6.3.3 -- swift --version' ]]; then printf 'Swift version 6.3.3 (swift-6.3.3-RELEASE)\\n'; exit 0; fi\nexit 2`,
  )

  return {
    developerDir,
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
        child.stdout.setEncoding('utf8').on('data', chunk => (stdout += chunk))
        child.stderr.setEncoding('utf8').on('data', chunk => (stderr += chunk))
        child.on('error', reject)
        child.on('close', code => resolve({ code, stdout, stderr }))
      })
    },
  }
}

test('requires the Xcode-selected host compiler and matching mise Swift', async t => {
  const f = await fixture(t)
  const result = await f.run()
  assert.equal(result.code, 0, result.stderr)
  assert.match(result.stdout, /Xcode Swift 6\.3\.3 and mise Swift 6\.3\.3/u)

  const workflow = await readFile(workflowPath, 'utf8')
  const androidJob = workflow.slice(
    workflow.indexOf('  test-swift-android:'),
    workflow.indexOf('  test-swift-ui:'),
  )
  assert.match(androidJob, /select-xcode\.sh 26\.6[\s\S]*?verify-android-host-swift\.sh/u)
  assert.doesNotMatch(androidJob, /DEVELOPER_DIR: \/Applications\/Xcode\.app/u)
})

test('rejects a non-matching Xcode even when mise has the requested compiler', async t => {
  const f = await fixture(t, { xcode: '27.0', swift: '6.4.0' })
  const result = await f.run()
  assert.equal(result.code, 1)
  assert.match(result.stderr, /require selected Xcode 26\.6/u)
})

test('rejects a mismatched compiler inside the selected Xcode', async t => {
  const f = await fixture(t, { swift: '6.3.2' })
  const result = await f.run()
  assert.equal(result.code, 1)
  assert.match(result.stderr, /require Xcode Swift 6\.3\.3/u)
})

test('rejects xcrun resolving Swift outside the selected Xcode', async t => {
  const f = await fixture(t, { swiftInsideXcode: false })
  const result = await f.run()
  assert.equal(result.code, 1)
  assert.match(result.stderr, /outside selected Xcode/u)
})
