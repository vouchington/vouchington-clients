const PROGRAMS = new Set([
  'emulator',
  'qemu-system-x86_64',
  'qemu-system-x86_64-headless',
  'crashpad_handler',
])

export const lineageLimits = Object.freeze({
  maxProcesses: 16,
  maxScanned: 4096,
  maxCommandBytes: 512,
  maxDurationMs: 1000,
  maxSerializedBytes: 4096,
})

export function ownedAvdName(runId) {
  if (typeof runId !== 'string' || !/^[0-9]{1,20}$/u.test(runId))
    throw new Error('workflow run ID cannot name an owned AVD')
  return `paired-native-${runId}`
}

export function assertOwnedAvd(runId, avdName) {
  if (avdName !== ownedAvdName(runId))
    throw new Error('Android session AVD is not the run-owned identity')
}

export async function waitForEmulatorBoot(runCommand, sleep) {
  runCommand('adb', ['wait-for-device'], { timeout: 120000 })
  for (let attempt = 0; attempt < 90; attempt += 1) {
    if (runCommand('adb', ['shell', 'getprop', 'sys.boot_completed'], { timeout: 10000 }) === '1')
      return
    await sleep(2000)
  }
  throw new Error('API 35 emulator did not finish booting')
}

export function parseProcStat(text) {
  if (typeof text !== 'string' || text.length === 0 || text.length > 1024) return null
  const open = text.indexOf('(')
  const close = text.lastIndexOf(')')
  if (open <= 0 || close <= open + 1) return null
  const pidText = text.slice(0, open).trim()
  if (!/^[1-9][0-9]*$/u.test(pidText)) return null
  const fields = text
    .slice(close + 1)
    .trim()
    .split(/\s+/u)
  if (fields.length < 20) return null
  const ppid = Number(fields[1])
  const pgid = Number(fields[2])
  const startTime = Number(fields[19])
  if (
    !Number.isSafeInteger(ppid) ||
    ppid < 0 ||
    !Number.isSafeInteger(pgid) ||
    pgid < 0 ||
    !Number.isSafeInteger(startTime) ||
    startTime < 0
  )
    return null
  return { pid: Number(pidText), ppid, pgid, startTime }
}

export function commandIdentity(bytes, avdName, maxCommandBytes) {
  if (!Buffer.isBuffer(bytes) || !Number.isSafeInteger(maxCommandBytes) || maxCommandBytes < 1)
    return { reason: 'malformed' }
  if (bytes.length === 0) return { reason: 'missing' }
  const over = bytes.length > maxCommandBytes
  const window = over ? bytes.subarray(0, maxCommandBytes) : bytes
  const end = over ? window.lastIndexOf(0) : window.length
  if (over && end <= 0) return { reason: 'truncated' }
  const tokens = []
  for (const token of window.subarray(0, end).toString('utf8').split('\0')) {
    if (token.length === 0) continue
    if (token.length > 160) return { reason: 'malformed' }
    tokens.push(token)
  }
  if (tokens.length === 0) return { reason: 'missing' }
  const program = tokens[0].split('/').at(-1)
  if (!PROGRAMS.has(program)) return { reason: 'ambiguous' }
  const avdFlag = tokens.indexOf('-avd')
  if (avdFlag === -1)
    return program === 'emulator'
      ? { reason: over ? 'truncated' : 'missing' }
      : { command: program }
  const value = tokens[avdFlag + 1]
  if (value === undefined) return { reason: 'truncated' }
  if (value !== avdName) return { reason: 'ambiguous' }
  return { command: `${program} -avd ${avdName}` }
}

function publicLineage(snapshot) {
  if (snapshot?.state !== 'observed') return snapshot
  return {
    state: 'observed',
    launcherPid: snapshot.launcherPid,
    processes: snapshot.processes,
  }
}

export function sessionCleanup({
  runId,
  nativeTestProfile,
  clientsMergeSha,
  avdName,
  launch,
  shutdown,
}) {
  return {
    schema: 1,
    runId,
    runAttempt: 1,
    nativeTestProfile,
    clientsMergeSha,
    avdName,
    launch: publicLineage(launch),
    shutdown: publicLineage(shutdown),
  }
}

export async function captureLineage(observe, options) {
  const limit = Number.isSafeInteger(options.limits?.maxDurationMs)
    ? options.limits.maxDurationMs
    : lineageLimits.maxDurationMs
  let timer
  const timeout = new Promise(resolve => {
    timer = setTimeout(() => {
      resolve({ state: 'unknown', launcherPid: options.launcherPid, reason: 'bounded' })
    }, limit)
    timer.unref?.()
  })
  const scan = Promise.resolve()
    .then(() => observe(options))
    .then(snapshot => {
      if (
        snapshot &&
        (snapshot.state === 'observed' || snapshot.state === 'unknown') &&
        snapshot.launcherPid === options.launcherPid
      )
        return snapshot
      return { state: 'unknown', launcherPid: options.launcherPid, reason: 'missing' }
    })
    .catch(() => ({ state: 'unknown', launcherPid: options.launcherPid, reason: 'missing' }))
  try {
    return await Promise.race([scan, timeout])
  } finally {
    clearTimeout(timer)
  }
}

export async function stopOwnedLauncher(child, sleep) {
  const exited = async milliseconds => {
    if (child.exitCode !== null || child.signalCode != null) return true
    return Promise.race([
      new Promise(resolve => child.once('exit', () => resolve(true))),
      sleep(milliseconds).then(() => false),
    ])
  }
  if (!(await exited(1000))) {
    try {
      child.kill('SIGTERM')
    } catch {
      /* try SIGKILL below */
    }
    if (!(await exited(5000))) {
      try {
        child.kill('SIGKILL')
      } catch {
        /* report if the owned process remains alive */
      }
      if (!(await exited(5000)))
        return new Error('owned emulator process did not exit after SIGKILL')
    }
  }
  return undefined
}
