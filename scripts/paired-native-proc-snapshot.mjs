import { opendir as openProcDirectory, readFile as readProcFile } from 'node:fs/promises'
import { commandIdentity, lineageLimits, parseProcStat } from './paired-native-emulator-lineage.mjs'
import { classifyOwnership, selectOwnedLineage } from './paired-native-ownership.mjs'

function unknown(launcherPid, reason) {
  return { state: 'unknown', launcherPid, reason }
}

function scanDeadline(milliseconds) {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), milliseconds)
  timer.unref?.()
  return {
    signal: controller.signal,
    cancel() {
      clearTimeout(timer)
    },
  }
}

async function readCapped(readFile, path, maxBytes, signal) {
  const bytes = await readFile(path, { signal })
  const buffer = Buffer.isBuffer(bytes) ? bytes : Buffer.from(bytes)
  return buffer.subarray(0, maxBytes)
}

function disappeared(error) {
  return error?.code === 'ENOENT' || error?.code === 'ESRCH'
}

function timedOut(error) {
  return error?.name === 'AbortError' || error?.name === 'TimeoutError'
}

async function readStatTable({ readFile, opendir, now, started, limits, signal }) {
  let directory
  try {
    directory = await opendir('/proc')
  } catch {
    return { available: false }
  }
  const records = []
  let scanned = 0
  let complete = true
  let scanReason = 'bounded'
  let malformed = false
  let closeError
  try {
    for await (const entry of directory) {
      if (signal.aborted || now() - started > limits.maxDurationMs) {
        complete = false
        break
      }
      if (!/^[1-9][0-9]*$/u.test(entry.name)) continue
      scanned += 1
      if (scanned > limits.maxScanned) {
        complete = false
        break
      }
      const pid = Number(entry.name)
      try {
        const stat = await readCapped(readFile, `/proc/${pid}/stat`, 1024, signal)
        if (stat.length >= 1024) {
          malformed = true
          break
        }
        const parsed = parseProcStat(stat.toString('utf8'))
        if (!parsed || parsed.pid !== pid) {
          malformed = true
          break
        }
        records.push(parsed)
      } catch (error) {
        if (timedOut(error)) {
          complete = false
          break
        }
        if (disappeared(error)) continue
        malformed = true
        break
      }
    }
  } catch {
    malformed = true
  } finally {
    try {
      await directory.close?.()
    } catch (error) {
      if (error?.code !== 'ERR_DIR_CLOSED') closeError = error
    }
  }
  if (closeError) throw closeError
  return { available: true, complete, scanReason, malformed, records }
}

export async function observeOwnedEmulatorLineage({
  launcherPid,
  avdName,
  priorOwnedPids,
  claimAbsent = false,
  limits = lineageLimits,
  now = Date.now,
  opendir = openProcDirectory,
  readFile = readProcFile,
  records,
  scanComplete,
  scanReason,
} = {}) {
  if (Array.isArray(records))
    return selectOwnedLineage({
      launcherPid,
      avdName,
      records,
      scanComplete,
      scanReason,
      priorOwnedPids,
      claimAbsent,
      limits,
    })
  const started = now()
  const deadline = scanDeadline(limits.maxDurationMs)
  try {
    const table = await readStatTable({
      readFile,
      opendir,
      now,
      started,
      limits,
      signal: deadline.signal,
    })
    if (!table.available) return unknown(launcherPid, 'missing')
    if (table.malformed) return unknown(launcherPid, 'malformed')
    const classified = classifyOwnership({
      launcherPid,
      records: table.records,
      scanComplete: table.complete,
      scanReason: table.scanReason,
      priorOwnedPids,
      claimAbsent,
      limits,
    })
    if (classified.reason) return unknown(launcherPid, classified.reason)
    if (classified.absent) return { state: 'observed', launcherPid, processes: [] }
    const owned = new Set(classified.pids)
    const withIdentity = []
    for (const record of table.records) {
      if (!owned.has(record.pid)) {
        withIdentity.push(record)
        continue
      }
      if (deadline.signal.aborted || now() - started > limits.maxDurationMs)
        return unknown(launcherPid, 'bounded')
      try {
        const command = await readCapped(
          readFile,
          `/proc/${record.pid}/cmdline`,
          limits.maxCommandBytes + 1,
          deadline.signal,
        )
        withIdentity.push({
          ...record,
          identity: commandIdentity(command, avdName, limits.maxCommandBytes),
        })
      } catch (error) {
        if (timedOut(error)) return unknown(launcherPid, 'bounded')
        return unknown(launcherPid, disappeared(error) ? 'missing' : 'malformed')
      }
    }
    return selectOwnedLineage({
      launcherPid,
      avdName,
      records: withIdentity,
      scanComplete: true,
      priorOwnedPids,
      claimAbsent,
      limits,
    })
  } finally {
    deadline.cancel()
  }
}
