import { opendir as openProcDirectory } from 'node:fs/promises'
import { commandIdentity, lineageLimits, parseProcStat } from './paired-native-emulator-lineage.mjs'
import { classifyOwnership, selectOwnedLineage } from './paired-native-ownership.mjs'
import { readBoundedFile, runBounded } from './paired-native-proc-read.mjs'

function unknown(launcherPid, reason) {
  return { state: 'unknown', launcherPid, reason }
}

async function readProcBounded(path, options = {}) {
  const length = Number.isSafeInteger(options.length) ? options.length : 1024
  return readBoundedFile(path, length, options.signal)
}

async function readCapped(readFile, path, maxBytes, signal) {
  if (signal?.aborted) {
    const error = new Error('lineage read aborted')
    error.name = 'AbortError'
    throw error
  }
  const bytes = await readFile(path, { signal, length: maxBytes })
  const buffer = Buffer.isBuffer(bytes) ? bytes : Buffer.from(bytes)
  return buffer.length > maxBytes ? buffer.subarray(0, maxBytes) : buffer
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
  } catch (error) {
    if (timedOut(error)) complete = false
    else malformed = true
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
  priorIdentities,
  claimAbsent = false,
  limits = lineageLimits,
  now = Date.now,
  opendir = openProcDirectory,
  readFile = readProcBounded,
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
      priorIdentities,
      claimAbsent,
      limits,
    })
  const started = now()
  const outcome = await runBounded(async signal => {
    const table = await readStatTable({
      readFile,
      opendir,
      now,
      started,
      limits,
      signal,
    })
    if (!table.available) return unknown(launcherPid, 'missing')
    if (table.malformed) return unknown(launcherPid, 'malformed')
    const classified = classifyOwnership({
      launcherPid,
      records: table.records,
      scanComplete: table.complete,
      scanReason: table.scanReason,
      priorIdentities,
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
      if (signal.aborted || now() - started > limits.maxDurationMs)
        return unknown(launcherPid, 'bounded')
      try {
        const command = await readCapped(
          readFile,
          `/proc/${record.pid}/cmdline`,
          limits.maxCommandBytes + 1,
          signal,
        )
        const stat = await readCapped(readFile, `/proc/${record.pid}/stat`, 1024, signal)
        if (stat.length >= 1024) return unknown(launcherPid, 'malformed')
        const again = parseProcStat(stat.toString('utf8'))
        if (
          !again ||
          again.pid !== record.pid ||
          again.ppid !== record.ppid ||
          again.pgid !== record.pgid ||
          again.startTime !== record.startTime
        )
          return unknown(launcherPid, again ? 'ambiguous' : 'malformed')
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
      priorIdentities,
      claimAbsent,
      limits,
    })
  }, limits.maxDurationMs)
  if (outcome.timedOut) return unknown(launcherPid, 'bounded')
  return outcome.value
}
