export function cleanupFixture({
  runId = '42',
  nativeTestProfile = 'pending-turn-recovery',
  clientsMergeSha = 'a'.repeat(40),
  launch,
  shutdown,
  launcherPid = 100,
} = {}) {
  const avdName = `paired-native-${runId}`
  return {
    schema: 1,
    runId,
    runAttempt: 1,
    nativeTestProfile,
    clientsMergeSha,
    avdName,
    launch: launch ?? {
      state: 'observed',
      launcherPid,
      processes: [
        {
          pid: launcherPid,
          ppid: 10,
          pgid: launcherPid,
          command: `emulator -avd ${avdName}`,
        },
      ],
    },
    shutdown: shutdown ?? { state: 'observed', launcherPid, processes: [] },
  }
}

export function cleanupBinding(overrides = {}) {
  return {
    runId: '42',
    runAttempt: 1,
    nativeTestProfile: 'pending-turn-recovery',
    clientsMergeSha: 'a'.repeat(40),
    runtimeArtifactId: 103,
    runtimeArtifactDigest: 'c'.repeat(64),
    boundRuntimeArtifactId: 103,
    boundRuntimeArtifactDigest: 'c'.repeat(64),
    ...overrides,
  }
}
