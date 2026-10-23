import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import {
  chmod,
  copyFile,
  mkdir,
  mkdtemp,
  readFile,
  realpath,
  rm,
  symlink,
  writeFile,
} from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const helper = new URL(
  '../swift-clients/apps/android/tooling/build-native-product.sh',
  import.meta.url,
)

async function fixture() {
  const root = await realpath(await mkdtemp(join(tmpdir(), 'voucha native product ')))
  const module = join(
    root,
    '.build/plugins/outputs/android/VouchaAndroid/destination/skipstone/VouchaAndroid',
  )
  const bin = join(root, 'bin')
  const trace = join(root, 'trace.json')
  await mkdir(join(module, 'src/main/swift'), { recursive: true })
  await mkdir(bin)
  await writeFile(join(module, 'src/main/swift/Package.swift'), '// generated package')
  await copyFile(
    new URL('fixtures/android-native-gradle-task.gradle.kts', import.meta.url),
    join(module, 'build.gradle.kts'),
  )
  const skip = join(bin, 'skip')
  await writeFile(
    skip,
    `#!/usr/bin/env node
const fs = require('node:fs');
fs.writeFileSync(process.env.TRACE, JSON.stringify({ args: process.argv.slice(2), cwd: process.cwd(), bridge: process.env.SKIP_BRIDGE, android: process.env.TARGET_OS_ANDROID, developer: process.env.DEVELOPER_DIR, override: process.env.SKIP_COMMAND_OVERRIDE }));
process.exit(Number(process.env.FAIL_NATIVE || 0));
`,
  )
  await chmod(skip, 0o755)
  const env = { ...process.env, PATH: `${bin}:${process.env.PATH}`, TRACE: trace }
  for (const key of [
    'SKIP_COMMAND_OVERRIDE',
    '__XCODE_BUILT_PRODUCTS_DIR_PATHS',
    'BUILT_PRODUCTS_DIR',
    'CONFIGURATION',
    'GITHUB_STEP_SUMMARY',
  ])
    delete env[key]
  return {
    root,
    module,
    skip,
    trace,
    run: (overrides = {}) =>
      spawnSync('bash', [helper.pathname, root], {
        env: { ...env, ...overrides },
        encoding: 'utf8',
        timeout: 10_000,
      }),
    observed: async () => JSON.parse(await readFile(trace, 'utf8')),
    clean: () => rm(root, { recursive: true, force: true }),
  }
}

test('direct product matches the generated native task argv, environment and working directory', async () => {
  const f = await fixture()
  try {
    const result = f.run({
      DEVELOPER_DIR: '/inherited/xcode',
      SKIP_BRIDGE: '0',
      TARGET_OS_ANDROID: '0',
    })
    assert.equal(result.status, 0, result.stderr)
    assert.deepEqual(await f.observed(), {
      args: [
        'android',
        'build',
        '-d',
        `${f.module}/build/jni-libs`,
        '--package-path',
        `${f.module}/src/main/swift`,
        '--configuration',
        'debug',
        '--product',
        'VouchaAndroid',
        '--scratch-path',
        `${f.module}/build/swift`,
        '--arch',
        'automatic',
        '-Xcc',
        '-fPIC',
        '-Xswiftc',
        '-DSKIP_BRIDGE',
        '-Xswiftc',
        '-DTARGET_OS_ANDROID',
        '-Xswiftc',
        '-Xfrontend',
        '-Xswiftc',
        '-no-clang-module-breadcrumbs',
        '-Xswiftc',
        '-module-cache-path',
        '-Xswiftc',
        `${f.module}/build/swift/module-cache/VouchaAndroid`,
      ],
      cwd: f.module,
      bridge: '1',
      android: '1',
      developer: '',
    })
    assert.equal(f.run({ FAIL_NATIVE: '43' }).status, 43)
  } finally {
    await f.clean()
  }
})

test('Skip executable selection preserves existing override and Xcode fallback semantics', async () => {
  const f = await fixture()
  try {
    await symlink(f.skip, join(f.module, 'override skip'))
    assert.equal(f.run({ SKIP_COMMAND_OVERRIDE: './override skip' }).status, 0)
    assert.equal((await f.observed()).override, './override skip')
    assert.equal(f.run({ SKIP_COMMAND_OVERRIDE: '/missing/skip' }).status, 0)
    assert.equal((await f.observed()).override, '/missing/skip')
    const products = join(f.root, 'Products')
    await mkdir(join(products, 'Release'), { recursive: true })
    await mkdir(join(products, 'Debug'))
    await symlink(f.skip, join(products, 'Release/skip'))
    assert.equal(
      f.run({ BUILT_PRODUCTS_DIR: join(products, 'Debug'), CONFIGURATION: 'Release' }).status,
      0,
    )
    assert.equal((await f.observed()).override, `${products}/Debug/../Release/skip`)
  } finally {
    await f.clean()
  }
})

test('missing generated source or changed task fails before Skip executes', async () => {
  const f = await fixture()
  try {
    const source = join(f.module, 'src/main/swift/Package.swift')
    await rm(source)
    assert.equal(f.run().status, 1)
    await assert.rejects(readFile(f.trace), { code: 'ENOENT' })
    await writeFile(source, '// restored')
    const gradle = join(f.module, 'build.gradle.kts')
    await writeFile(
      gradle,
      (await readFile(gradle, 'utf8')).replace('--arch automatic', '--arch aarch64'),
    )
    const changed = f.run()
    assert.equal(changed.status, 1)
    assert.match(changed.stderr, /review direct invocation parity/u)
    await assert.rejects(readFile(f.trace), { code: 'ENOENT' })
  } finally {
    await f.clean()
  }
})
