import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { chmod, mkdir, mkdtemp, readFile, realpath, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import test from 'node:test'

const exec = promisify(execFile)
const script = fileURLToPath(new URL('../dotnet-clients/tooling/select-xcode.sh', import.meta.url))

async function executable(path, contents) {
  await writeFile(path, `#!/bin/bash\n${contents}\n`)
  await chmod(path, 0o755)
}

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), 'voucha-xcode-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const applications = join(root, 'Applications with spaces')
  const bin = join(root, 'bin')
  await mkdir(applications)
  await mkdir(bin)
  await executable(join(bin, 'xcode-select'), 'exit 1')
  await executable(
    join(bin, 'xcrun'),
    `case "$*" in
  '--sdk macosx --show-sdk-path')
    [[ ! -f "$DEVELOPER_DIR/reject-sdk" ]] || exit 1
    printf '%s\\n' "$DEVELOPER_DIR/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk" ;;
  "--sdk $DEVELOPER_DIR/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk --find actool") printf '%s\\n' "$DEVELOPER_DIR/usr/bin/actool" ;;
  *) exit 2 ;;
esac`,
  )
  const environmentFile = join(root, 'github-env')
  await writeFile(environmentFile, '')
  return {
    applications,
    environmentFile,
    async xcode(name, version, { catalyst = true, actool = true } = {}) {
      const developer = join(applications, name, 'Contents/Developer')
      await mkdir(join(developer, 'usr/bin'), { recursive: true })
      await executable(join(developer, 'usr/bin/xcodebuild'), `printf 'Xcode ${version}\\n'`)
      const sdk = join(developer, 'Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk')
      await mkdir(sdk, { recursive: true })
      await writeFile(join(sdk, 'SDKSettings.plist'), '')
      if (catalyst) await mkdir(join(sdk, 'System/iOSSupport'), { recursive: true })
      if (actool) await executable(join(developer, 'usr/bin/actool'), 'exit 0')
      return developer
    },
    run: () =>
      exec('/bin/bash', [script, '26.5', applications], {
        env: { ...process.env, PATH: `${bin}:${process.env.PATH}`, GITHUB_ENV: environmentFile },
      }),
  }
}

test('selects the required Xcode using Mac Catalyst support inside the macOS SDK', async t => {
  const f = await fixture(t)
  await f.xcode('Xcode_26.6.app', '26.6')
  const developer = await f.xcode('Xcode_26.5.app', '26.5')
  await f.run()
  assert.equal(
    await readFile(f.environmentFile, 'utf8'),
    `DEVELOPER_DIR=${await realpath(developer)}\n`,
  )
})

test('fails the job when the required Xcode is unavailable instead of skipping its build', async t => {
  const f = await fixture(t)
  await f.xcode('Xcode_26.6.app', '26.6')
  await f.xcode('Xcode_26.50.app', '26.50')
  await assert.rejects(f.run(), error => error.code === 1 && /is required/u.test(error.stderr))
  assert.equal(await readFile(f.environmentFile, 'utf8'), '')
})

test('rejects an SDK without Mac Catalyst support', async t => {
  const f = await fixture(t)
  await f.xcode('Xcode_26.5.app', '26.5', { catalyst: false })
  await assert.rejects(f.run(), { code: 1 })
})

test('rejects a failed xcrun SDK lookup', async t => {
  const f = await fixture(t)
  const developer = await f.xcode('Xcode_26.5.app', '26.5')
  await writeFile(join(developer, 'reject-sdk'), '')
  await assert.rejects(f.run(), { code: 1 })
})

test('rejects a missing asset compiler', async t => {
  const f = await fixture(t)
  await f.xcode('Xcode_26.5.app', '26.5', { actool: false })
  await assert.rejects(f.run(), { code: 1 })
})

test('continues past an incomplete installation and accepts a matching patch version', async t => {
  const f = await fixture(t)
  await f.xcode('Xcode_26.5.app', '26.5', { catalyst: false })
  const developer = await f.xcode('Xcode_26.5.1.app', '26.5.1')
  await f.run()
  assert.equal(
    await readFile(f.environmentFile, 'utf8'),
    `DEVELOPER_DIR=${await realpath(developer)}\n`,
  )
})

test('resolves hosted Xcode aliases before exporting the toolchain path', async t => {
  const f = await fixture(t)
  const developer = await f.xcode('Toolchain.app', '26.5')
  await symlink(join(f.applications, 'Toolchain.app'), join(f.applications, 'Xcode_26.5.0.app'))
  await f.run()
  assert.equal(
    await readFile(f.environmentFile, 'utf8'),
    `DEVELOPER_DIR=${await realpath(developer)}\n`,
  )
})
