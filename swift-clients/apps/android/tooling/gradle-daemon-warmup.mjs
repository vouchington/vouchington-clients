import { spawn } from 'node:child_process'
import { accessSync, closeSync, constants, copyFileSync, existsSync, mkdirSync, openSync, readFileSync, renameSync, writeFileSync, writeSync } from 'node:fs'
import { basename, dirname, isAbsolute, join, resolve, sep } from 'node:path'

const pause = ms => new Promise(resolvePause => setTimeout(resolvePause, ms))
const logLimit = 65536
const grace = 1000

function options(argv) {
  const [command, ...flags] = argv
  if (!['start', 'join', 'worker'].includes(command) || flags.length % 2) throw Error('Expected start|join and named arguments')
  const args = {}
  for (let i = 0; i < flags.length; i += 2) {
    const name = flags[i]
    if (!['--state-dir', '--wrapper', '--properties', '--timeout-seconds'].includes(name) || name in args) throw Error(`Invalid argument: ${name}`)
    args[name] = flags[i + 1]
  }
  const temp = process.env.RUNNER_TEMP
  if (!temp || !isAbsolute(temp) || !args['--state-dir']) throw Error('RUNNER_TEMP and --state-dir are required')
  const state = resolve(args['--state-dir'])
  if (dirname(state) !== resolve(temp) || basename(state) === '.') throw Error('State must be a direct child of RUNNER_TEMP')
  return { command, args, state, temp: resolve(temp) }
}

function status(state, result) {
  writeFileSync(join(state, 'status.tmp'), JSON.stringify(result))
  renameSync(join(state, 'status.tmp'), join(state, 'status.json'))
}

function readStatus(state) {
  try { return JSON.parse(readFileSync(join(state, 'status.json'), 'utf8')) }
  catch (error) { if (error.code === 'ENOENT') return null; throw error }
}

async function start({ args, state, temp }) {
  const timeout = Number(args['--timeout-seconds'])
  if (!Number.isInteger(timeout) || timeout < 1 || timeout > 90) throw Error('Timeout must be an integer from 1 to 90 seconds')
  const home = process.env.VOUCHA_SKIP_SWIFT_HOME
  const java = process.env.JAVA_HOME
  const gradleHome = process.env.GRADLE_USER_HOME
  if (!home || !isAbsolute(home) || !resolve(home).startsWith(`${temp}${sep}`)) throw Error('VOUCHA_SKIP_SWIFT_HOME must be inside RUNNER_TEMP')
  if (!java || !isAbsolute(java) || !gradleHome || !isAbsolute(gradleHome)) throw Error('Explicit JAVA_HOME and GRADLE_USER_HOME are required')
  accessSync(join(java, 'bin/java'), constants.X_OK)
  if (!args['--wrapper'] || !args['--properties']) throw Error('Wrapper and properties are required')
  const wrapper = resolve(args['--wrapper'])
  const properties = resolve(args['--properties'])
  // Match pre-push.sh before verified state materializes this future executable.
  const skipBin = join(dirname(dirname(wrapper)), '.build/artifacts/skip/skip/skip.artifactbundle/bin')
  accessSync(wrapper, constants.X_OK)
  accessSync(properties, constants.R_OK)
  mkdirSync(state)
  const project = join(state, 'project')
  mkdirSync(project)
  copyFileSync(properties, join(project, 'gradle.properties'))
  writeFileSync(join(project, 'settings.gradle'), '')
  writeFileSync(join(project, 'build.gradle'), '')
  mkdirSync(home, { recursive: true })
  const tmp = join(temp, 'swift-tmp')
  mkdirSync(tmp, { recursive: true })
  const config = { wrapper, properties, project, deadline: Date.now() + timeout * 1000 }
  writeFileSync(join(state, 'config.json'), JSON.stringify(config))
  const worker = spawn(process.execPath, [import.meta.filename, 'worker', '--state-dir', state], {
    detached: true, stdio: 'ignore',
    env: { ...process.env, HOME: home, CFFIXED_USER_HOME: home, TMPDIR: tmp, PATH: `${skipBin}:${process.env.PATH || ''}` },
  })
  let spawnError
  worker.on('error', error => { spawnError = error })
  worker.unref()
  const acknowledgement = Math.min(config.deadline, Date.now() + 3000)
  while (!readStatus(state)) {
    if (spawnError) throw spawnError
    if (Date.now() >= acknowledgement) {
      worker.kill('SIGTERM')
      throw Error('Warm-up worker did not acknowledge startup')
    }
    await pause(20)
  }
  console.log('Gradle daemon warm-up started against an empty temporary project')
}

async function worker(state) {
  const config = JSON.parse(readFileSync(join(state, 'config.json'), 'utf8'))
  const log = openSync(join(state, 'gradle.log.tmp'), 'wx')
  let output = Buffer.alloc(0)
  let truncated = false
  const capture = chunk => {
    const next = Buffer.concat([output, Buffer.from(chunk)])
    truncated ||= next.length > logLimit
    output = next.subarray(-logLimit)
  }
  status(state, { phase: 'running', workerPid: process.pid })
  const child = spawn(config.wrapper, ['--daemon', '--project-dir', config.project, '--console=plain', 'help'], {
    cwd: config.project, detached: true, stdio: ['ignore', 'pipe', 'pipe'],
  })
  child.stdout.on('data', capture)
  child.stderr.on('data', capture)
  let forcedCode
  let reason
  let cleanup
  const signalGroup = signal => {
    if (!child.pid) return
    try { process.kill(-child.pid, signal) }
    catch (error) { if (error.code !== 'ESRCH') throw error }
  }
  const stop = (code, message) => {
    if (forcedCode) return
    forcedCode = code
    reason = message
    signalGroup('SIGTERM')
    cleanup = new Promise(resolveCleanup => setTimeout(() => { signalGroup('SIGKILL'); resolveCleanup() }, Math.min(500, Math.max(0, config.deadline - Date.now()))))
  }
  // Reserve cleanup inside the absolute deadline; never stop unrelated daemons.
  const timeout = setTimeout(() => stop(124, 'Gradle warm-up deadline exceeded'), Math.max(0, config.deadline - Date.now() - 500))
  const onTerm = () => stop(143, 'Gradle warm-up worker interrupted')
  process.once('SIGTERM', onTerm)
  process.once('SIGINT', onTerm)
  const code = await new Promise(resolveCode => {
    child.once('error', error => { reason = error.message })
    child.once('exit', exit => { if (exit !== 0 && !forcedCode) stop(exit || 1, 'Gradle warm-up failed') })
    child.once('close', exit => resolveCode(forcedCode || (Number.isInteger(exit) && exit >= 0 ? exit : 1)))
  })
  clearTimeout(timeout)
  if (cleanup) await cleanup
  writeSync(log, output)
  closeSync(log)
  renameSync(join(state, 'gradle.log.tmp'), join(state, 'gradle.log'))
  status(state, { phase: 'done', code, reason, truncated })
}

async function joinWorker({ args, state }) {
  const config = JSON.parse(readFileSync(join(state, 'config.json'), 'utf8'))
  for (const [flag, key] of [['--wrapper', 'wrapper'], ['--properties', 'properties']]) {
    if (args[flag] && resolve(args[flag]) !== config[key]) throw Error('Warm-up inputs do not match startup')
  }
  let result
  while ((result = readStatus(state))?.phase !== 'done') {
    if (Date.now() > config.deadline + grace) throw Error('Warm-up worker did not publish a terminal deadline result')
    await pause(50)
  }
  if (existsSync(join(state, 'gradle.log'))) process.stdout.write(readFileSync(join(state, 'gradle.log')))
  if (result.truncated) console.log('\nGradle warm-up log truncated to its final 64 KiB')
  if (result.code !== 0) console.error(result.reason || 'Gradle warm-up failed')
  process.exitCode = result.code
}

try {
  const parsed = options(process.argv.slice(2))
  if (parsed.command === 'start') await start(parsed)
  else if (parsed.command === 'worker') await worker(parsed.state)
  else await joinWorker(parsed)
} catch (error) {
  console.error(error.message)
  process.exitCode = 1
}
