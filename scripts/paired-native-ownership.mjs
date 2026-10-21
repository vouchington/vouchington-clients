function validStat(record) {
  return (
    Number.isSafeInteger(record?.pid) &&
    record.pid > 0 &&
    Number.isSafeInteger(record.ppid) &&
    record.ppid >= 0 &&
    Number.isSafeInteger(record.pgid) &&
    record.pgid >= 0
  )
}

function parentCycle(pid, byPid, limit) {
  const seen = new Set()
  let current = pid
  for (let depth = 0; depth <= limit; depth += 1) {
    if (seen.has(current)) return true
    seen.add(current)
    const record = byPid.get(current)
    if (!record || record.ppid === 0) return false
    current = record.ppid
  }
  return true
}

function allowedCommand(command, avdName) {
  if (command === `emulator -avd ${avdName}`) return true
  if (command === `qemu-system-x86_64 -avd ${avdName}`) return true
  if (command === `qemu-system-x86_64-headless -avd ${avdName}`) return true
  return (
    command === 'qemu-system-x86_64' ||
    command === 'qemu-system-x86_64-headless' ||
    command === 'crashpad_handler'
  )
}

export function classifyOwnership({
  launcherPid,
  records,
  scanComplete = true,
  scanReason = 'bounded',
  priorOwnedPids = [],
  claimAbsent = false,
  limits,
}) {
  if (!Number.isSafeInteger(launcherPid) || launcherPid <= 0) return { reason: 'malformed' }
  if (!Number.isSafeInteger(limits?.maxProcesses) || limits.maxProcesses < 1)
    return { reason: 'malformed' }
  if (!scanComplete) return { reason: scanReason }
  if (!Array.isArray(records) || !Array.isArray(priorOwnedPids)) return { reason: 'malformed' }
  if (records.length > limits.maxScanned || priorOwnedPids.length > limits.maxProcesses)
    return { reason: 'bounded' }
  const byPid = new Map()
  for (const record of records) {
    if (!validStat(record)) return { reason: 'malformed' }
    if (byPid.has(record.pid)) return { reason: 'ambiguous' }
    byPid.set(record.pid, record)
  }
  for (const pid of priorOwnedPids)
    if (!Number.isSafeInteger(pid) || pid <= 0) return { reason: 'malformed' }
  const children = new Map()
  for (const record of byPid.values()) {
    const list = children.get(record.ppid)
    if (list) list.push(record.pid)
    else children.set(record.ppid, [record.pid])
  }
  const seeds = new Set()
  if (byPid.has(launcherPid)) seeds.add(launcherPid)
  for (const pid of priorOwnedPids) if (byPid.has(pid)) seeds.add(pid)
  for (const record of byPid.values()) if (record.ppid === launcherPid) seeds.add(record.pid)
  for (const pid of seeds)
    if (parentCycle(pid, byPid, limits.maxProcesses)) return { reason: 'ambiguous' }
  const owned = []
  const seen = new Set()
  const stack = [...seeds]
  while (stack.length > 0) {
    const pid = stack.pop()
    if (seen.has(pid)) continue
    seen.add(pid)
    if (owned.length === limits.maxProcesses) return { reason: 'bounded' }
    owned.push(pid)
    for (const child of children.get(pid) ?? []) stack.push(child)
  }
  if (owned.length === 0) return claimAbsent ? { absent: true } : { reason: 'missing' }
  owned.sort((left, right) => left - right)
  return { pids: owned }
}

export function selectOwnedLineage({
  launcherPid,
  avdName,
  records,
  scanComplete,
  scanReason,
  priorOwnedPids,
  claimAbsent,
  limits,
}) {
  const classified = classifyOwnership({
    launcherPid,
    records,
    scanComplete,
    scanReason,
    priorOwnedPids,
    claimAbsent,
    limits,
  })
  if (classified.reason) return { state: 'unknown', launcherPid, reason: classified.reason }
  if (classified.absent) return { state: 'observed', launcherPid, processes: [] }
  const byPid = new Map(records.map(record => [record.pid, record]))
  const processes = []
  for (const pid of classified.pids) {
    const record = byPid.get(pid)
    const command = record?.identity?.command
    if (!record?.identity || record.identity.reason || typeof command !== 'string')
      return {
        state: 'unknown',
        launcherPid,
        reason: record?.identity?.reason ?? 'missing',
      }
    if (!allowedCommand(command, avdName))
      return {
        state: 'unknown',
        launcherPid,
        reason: / -avd paired-native-[0-9]{1,20}$/u.test(command) ? 'ambiguous' : 'malformed',
      }
    if (pid === launcherPid && command !== `emulator -avd ${avdName}`)
      return { state: 'unknown', launcherPid, reason: 'ambiguous' }
    processes.push({ pid, ppid: record.ppid, pgid: record.pgid, command })
  }
  const snapshot = { state: 'observed', launcherPid, processes }
  if (Buffer.byteLength(JSON.stringify(snapshot)) > limits.maxSerializedBytes)
    return { state: 'unknown', launcherPid, reason: 'bounded' }
  return snapshot
}
