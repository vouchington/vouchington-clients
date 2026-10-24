import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import {
  chmod,
  copyFile,
  mkdir,
  mkdtemp,
  readFile,
  readdir,
  realpath,
  rm,
  writeFile,
} from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const helper = new URL(
  '../swift-clients/apps/android/tooling/gradle-daemon-warmup.mjs',
  import.meta.url,
)
const properties = new URL(
  '../swift-clients/apps/android/Android/gradle.properties',
  import.meta.url,
)
const pause = ms => new Promise(resolve => setTimeout(resolve, ms))
const alive = pid => {
  try {
    process.kill(pid, 0)
    return true
  } catch (error) {
    if (error.code === 'ESRCH') return false
    throw error
  }
}

async function waitFor(predicate) {
  const end = Date.now() + 3000
  while (!(await predicate())) {
    assert.ok(Date.now() < end, 'owned process did not reach the expected state')
    await pause(20)
  }
}

async function fixture(t, body, timeout = 5) {
  const root = await realpath(await mkdtemp(join(tmpdir(), 'gradle-warmup-')))
  const android = join(root, 'package', 'Android')
  const runner = join(root, 'runner')
  const java = join(root, 'java')
  const state = join(runner, 'warmup')
  const home = join(runner, 'skip-swift-home')
  const wrapper = join(android, 'gradlew')
  const props = join(android, 'gradle.properties')
  const script = join(root, 'gradle-daemon-warmup.mjs')
  const record = join(root, 'record.json')
  for (const dir of [android, runner, join(java, 'bin')]) await mkdir(dir, { recursive: true })
  await copyFile(helper, script)
  await copyFile(properties, props)
  await writeFile(join(java, 'bin/java'), '#!/bin/sh\nexit 0\n')
  await chmod(join(java, 'bin/java'), 0o755)
  await writeFile(join(android, 'settings.gradle'), 'REAL PROJECT MUST NOT BE CONFIGURED\n')
  await writeFile(join(android, 'build.gradle'), 'REAL BUILD MUST NOT RUN\n')
  await writeFile(
    wrapper,
    `#!/usr/bin/env node
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { spawn, spawnSync } from 'node:child_process'
const args = process.argv.slice(2)
const project = args[args.indexOf('--project-dir') + 1]
writeFileSync(process.env.RECORD, JSON.stringify({
  pid: process.pid, args, cwd: process.cwd(), path: process.env.PATH,
  home: process.env.HOME, fixed: process.env.CFFIXED_USER_HOME,
  temp: process.env.TMPDIR, java: process.env.JAVA_HOME, gradle: process.env.GRADLE_USER_HOME,
  properties: readFileSync(project + '/gradle.properties', 'utf8'),
  settings: readFileSync(project + '/settings.gradle', 'utf8'),
  build: readFileSync(project + '/build.gradle', 'utf8'),
}))
${body}
`,
  )
  await chmod(wrapper, 0o755)
  const env = {
    ...process.env,
    RUNNER_TEMP: runner,
    JAVA_HOME: java,
    GRADLE_USER_HOME: join(root, 'gradle-home'),
    VOUCHA_SKIP_SWIFT_HOME: home,
    RECORD: record,
    RELEASE: join(root, 'release'),
    CHILD_PID: join(root, 'child.pid'),
  }
  t.after(async () => {
    try {
      const data = JSON.parse(await readFile(record, 'utf8'))
      if (alive(data.pid)) process.kill(-data.pid, 'SIGKILL')
    } catch (error) {
      if (!['ENOENT', 'ESRCH'].includes(error.code)) throw error
    }
    await rm(root, { recursive: true, force: true })
  })
  const run = (command, overrides = {}) =>
    new Promise((resolve, reject) => {
      const child = spawn(
        process.execPath,
        [
          script,
          command,
          '--state-dir',
          state,
          '--wrapper',
          wrapper,
          '--properties',
          props,
          '--timeout-seconds',
          String(timeout),
        ],
        { env: { ...env, ...overrides } },
      )
      let stdout = '',
        stderr = ''
      child.stdout.on('data', data => {
        stdout += data
      })
      child.stderr.on('data', data => {
        stderr += data
      })
      child.once('error', reject)
      child.once('close', code => resolve({ code, stdout, stderr }))
    })
  return { root, android, runner, state, home, java, wrapper, props, record, env, run }
}

test('warms only an empty project with exact properties and later build environment, then strictly joins', async t => {
  const f = await fixture(
    t,
    `const waiting = setInterval(() => {
  if (existsSync(process.env.RELEASE)) { clearInterval(waiting); console.log('daemon ready') }
}, 10)`,
  )
  const started = await f.run('start')
  assert.equal(started.code, 0, started.stderr)
  await waitFor(async () => {
    try {
      await readFile(f.record)
      return true
    } catch {
      return false
    }
  })
  const record = JSON.parse(await readFile(f.record, 'utf8'))
  assert.deepEqual(record.args, [
    '--daemon',
    '--project-dir',
    join(f.state, 'project'),
    '--console=plain',
    'help',
  ])
  assert.equal(record.cwd, join(f.state, 'project'))
  assert.equal(record.properties, await readFile(properties, 'utf8'))
  assert.equal(record.settings, '')
  assert.equal(record.build, '')
  assert.equal(record.home, f.home)
  assert.equal(record.fixed, f.home)
  assert.equal(record.temp, join(f.runner, 'swift-tmp'))
  assert.equal(record.java, f.java)
  assert.equal(record.gradle, f.env.GRADLE_USER_HOME)
  assert.equal(JSON.parse(await readFile(join(f.state, 'status.json'), 'utf8')).phase, 'running')
  await writeFile(f.env.RELEASE, '')
  const joined = await f.run('join')
  assert.equal(joined.code, 0, joined.stderr)
  assert.match(joined.stdout, /daemon ready/u)
  assert.equal(
    await readFile(join(f.android, 'settings.gradle'), 'utf8'),
    'REAL PROJECT MUST NOT BE CONFIGURED\n',
  )
  assert.equal(await readFile(join(f.android, 'build.gradle'), 'utf8'), 'REAL BUILD MUST NOT RUN\n')
  assert.equal(await readFile(f.props, 'utf8'), await readFile(properties, 'utf8'))
  assert.deepEqual((await readdir(f.android)).sort(), [
    'build.gradle',
    'gradle.properties',
    'gradlew',
    'settings.gradle',
  ])
})

test('join propagates the wrapper failure and its diagnostic', async t => {
  const f = await fixture(
    t,
    `const child = spawn(process.execPath, ['-e', "const fs=require('node:fs');process.on('SIGTERM',()=>{});fs.writeFileSync(process.env.CHILD_PID+'.ready','');setInterval(()=>{},1000)"], { stdio: 'ignore' })
child.unref()
writeFileSync(process.env.CHILD_PID, String(child.pid))
const waiting = setInterval(() => {
  if (existsSync(process.env.CHILD_PID + '.ready')) {
    clearInterval(waiting)
    console.error('fixture failed')
    process.exitCode = 23
  }
}, 10)`,
  )
  assert.equal((await f.run('start')).code, 0)
  const result = await f.run('join')
  assert.equal(result.code, 23)
  assert.match(result.stdout, /fixture failed/u)
  assert.match(result.stderr, /Gradle warm-up failed/u)
  const childPid = Number(await readFile(f.env.CHILD_PID, 'utf8'))
  await waitFor(() => !alive(childPid))
})

test('deadline terminates only the owned wrapper process group, including a stubborn child', async t => {
  const f = await fixture(
    t,
    `const child = spawn(process.execPath, ['-e', "process.on('SIGTERM',()=>{});setInterval(()=>{},1000)"], { stdio: 'ignore' })
writeFileSync(process.env.CHILD_PID, String(child.pid))
process.on('SIGTERM', () => {})
setInterval(() => {}, 1000)`,
    1,
  )
  const sentinel = spawn(process.execPath, ['-e', 'setInterval(()=>{},1000)'], { stdio: 'ignore' })
  t.after(() => sentinel.kill())
  assert.equal((await f.run('start')).code, 0)
  const result = await f.run('join')
  assert.equal(result.code, 124, result.stderr)
  assert.match(result.stderr, /deadline exceeded/u)
  const wrapperPid = JSON.parse(await readFile(f.record, 'utf8')).pid
  const childPid = Number(await readFile(f.env.CHILD_PID, 'utf8'))
  await waitFor(() => !alive(wrapperPid) && !alive(childPid))
  assert.ok(alive(sentinel.pid), 'unrelated process must survive')
})

test('caps background output and fails closed on missing state, invalid context, and reused state', async t => {
  const f = await fixture(t, "process.stdout.write('x'.repeat(100000))")
  assert.notEqual((await f.run('join')).code, 0)
  assert.notEqual((await f.run('start', { GRADLE_USER_HOME: '' })).code, 0)
  assert.equal((await f.run('start')).code, 0)
  const result = await f.run('join')
  assert.equal(result.code, 0, result.stderr)
  assert.equal((await readFile(join(f.state, 'gradle.log'))).length, 65536)
  assert.match(result.stdout, /log truncated to its final 64 KiB/u)
  const before = await readFile(join(f.state, 'config.json'), 'utf8')
  assert.notEqual((await f.run('start')).code, 0)
  assert.equal(await readFile(join(f.state, 'config.json'), 'utf8'), before)
})

test('rejects deadlines above the bounded limit before starting a wrapper', async t => {
  const f = await fixture(t, "throw Error('must never run')", 91)
  const result = await f.run('start')
  assert.notEqual(result.code, 0)
  assert.match(result.stderr, /1 to 90 seconds/u)
  await assert.rejects(readFile(f.record), { code: 'ENOENT' })
})

test('retains the terminal diagnostic after large failing output and propagates its exit', async t => {
  const f = await fixture(
    t,
    "process.stderr.write('old output\\n'.repeat(15000) + 'PRIMARY TERMINAL FAILURE\\n'); process.exitCode = 37",
  )
  assert.equal((await f.run('start')).code, 0)
  const result = await f.run('join')
  assert.equal(result.code, 37)
  assert.match(result.stdout, /PRIMARY TERMINAL FAILURE/u)
  const log = await readFile(join(f.state, 'gradle.log'), 'utf8')
  assert.equal(Buffer.byteLength(log), 65536)
  assert.ok(log.endsWith('PRIMARY TERMINAL FAILURE\n'))
})

test('startup retains future Skip and mise Swift paths when verified artifacts arrive later', async t => {
  const f = await fixture(
    t,
    `const waiting = setInterval(() => {
  if (!existsSync(process.env.RELEASE)) return
  clearInterval(waiting)
  for (const executable of ['skip', 'swift']) {
    const result = spawnSync(executable, [], { encoding: 'utf8' })
    if (result.error || result.status !== 0) throw result.error || Error(executable + ' failed')
    console.log(executable + ': ' + result.stdout.trim())
  }
}, 10)`,
  )
  const futureSkip = join(f.root, 'package/.build/artifacts/skip/skip/skip.artifactbundle/bin')
  const miseSwift = join(f.root, 'mise-pinned-swift/bin')
  const ambient = join(f.root, 'ambient/bin')
  for (const dir of [miseSwift, ambient]) await mkdir(dir, { recursive: true })
  for (const [dir, label] of [
    [miseSwift, 'pinned mise Swift'],
    [ambient, 'wrong ambient Swift'],
  ]) {
    await writeFile(join(dir, 'swift'), `#!/bin/sh\necho '${label}'\n`)
    await chmod(join(dir, 'swift'), 0o755)
  }
  await assert.rejects(readFile(join(futureSkip, 'skip')), { code: 'ENOENT' })
  const inheritedPath = `${miseSwift}:${ambient}:${process.env.PATH}`
  assert.equal((await f.run('start', { PATH: inheritedPath })).code, 0)
  await waitFor(async () => {
    try {
      await readFile(f.record)
      return true
    } catch {
      return false
    }
  })
  const record = JSON.parse(await readFile(f.record, 'utf8'))
  assert.equal(record.path, `${futureSkip}:${inheritedPath}`)
  assert.equal(record.args.at(-1), 'help')
  assert.equal(record.build, '')
  assert.equal(record.settings, '')
  // Startup succeeded before Skip existed; the same child environment resolves it later.
  await assert.rejects(readFile(join(futureSkip, 'skip')), { code: 'ENOENT' })
  await mkdir(futureSkip, { recursive: true })
  await writeFile(join(futureSkip, 'skip'), '#!/bin/sh\necho verified Skip artifact\n')
  await chmod(join(futureSkip, 'skip'), 0o755)
  await writeFile(f.env.RELEASE, '')
  const result = await f.run('join', { PATH: inheritedPath })
  assert.equal(result.code, 0, result.stderr)
  assert.match(result.stdout, /skip: verified Skip artifact/u)
  assert.match(result.stdout, /swift: pinned mise Swift/u)
  assert.doesNotMatch(result.stdout, /wrong ambient Swift/u)
})
