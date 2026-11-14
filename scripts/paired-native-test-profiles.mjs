const allAndroidPhases = Object.freeze([
  'CookieSnapshotBridgeRestartTest#seedCheckedEntries',
  'CookieSnapshotBridgeRestartTest#readEntriesAfterRestart',
  'CookieSnapshotBridgeRestartTest#removeOnlyCookieWithCheckedCommit',
  'CookieSnapshotBridgeRestartTest#readRemovalAfterRestart',
  'PendingTurnStoreBridgeRestartTest#seedCheckedOwnerRecords',
  'PendingTurnStoreBridgeRestartTest#readExactRecordsAfterRestart',
  'PendingTurnStoreBridgeRestartTest#deleteOnlyMatchingOwnerRecord',
  'PendingTurnStoreBridgeRestartTest#readTargetedDeletionAfterRestart',
])

export const androidTestProfiles = Object.freeze({
  'cookie-snapshot': Object.freeze(allAndroidPhases.slice(0, 4)),
  'pending-turn-recovery': allAndroidPhases,
})

export const androidPhases = allAndroidPhases

export function androidPhasesForProfile(profile) {
  if (typeof profile !== 'string' || !Object.hasOwn(androidTestProfiles, profile))
    throw new Error(
      'Paired native test evidence rejected: native test profile is not in the fixed allowlist',
    )
  return androidTestProfiles[profile]
}
