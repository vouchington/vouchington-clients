import { lineageLimits, ownedAvdName } from '../scripts/paired-native-emulator-lineage.mjs'
import { selectOwnedLineage } from '../scripts/paired-native-ownership.mjs'

// Both lineage tests observe this same run-owned AVD.
export const lineageFixtureAvd = ownedAvdName('55')

// /proc/PID/stat text that parseProcStat accepts. After the comm token, ppid,
// pgid, and starttime sit at indexes 1, 2, and 19; the other fields stay zero.
export function procStatText(pid, ppid, pgid, startTime, comm = 'emulator') {
  const fields = Array.from({ length: 20 }, () => '0')
  fields[0] = 'S'
  fields[1] = String(ppid)
  fields[2] = String(pgid)
  fields[19] = String(startTime)
  return `${pid} (${comm}) ${fields.join(' ')}\n`
}

// Caller options spread last, so a test can replace scan completion or limits.
export function selectLineageFixture(records, options = {}) {
  return selectOwnedLineage({
    launcherPid: 200,
    avdName: lineageFixtureAvd,
    records,
    scanComplete: true,
    limits: lineageLimits,
    ...options,
  })
}
