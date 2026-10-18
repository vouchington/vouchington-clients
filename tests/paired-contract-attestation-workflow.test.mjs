import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { it } from 'node:test'
import { runPairedContractCommand } from '../scripts/paired-contract-attestation.mjs'

const workflowPath = '../.github/workflows/paired-native-contract-attestation.yml'
const readWorkflow = () => readFile(new URL(workflowPath, import.meta.url), 'utf8')

function jobBlock(workflow, name) {
  const marker = `  ${name}:\n`
  const start = workflow.indexOf(marker)
  assert.notEqual(start, -1, `missing job ${name}`)
  const remaining = workflow.slice(start + marker.length)
  const next = remaining.search(/\n  [a-z][\w-]*:\n/u)
  return workflow.slice(start, next === -1 ? undefined : start + marker.length + next)
}

function stepBlock(workflow, name) {
  const marker = `      - name: ${name}`
  const start = workflow.indexOf(marker)
  assert.notEqual(start, -1, `missing step ${name}`)
  const next = workflow.indexOf('\n      - name:', start + marker.length)
  return workflow.slice(start, next === -1 ? undefined : next)
}

it('keeps paired review evidence separate from the required native contract workflow', async () => {
  const [workflow, existingWorkflow] = await Promise.all([
    readWorkflow(),
    readFile(new URL('../.github/workflows/native-contract-tests.yml', import.meta.url), 'utf8'),
  ])
  assert.match(workflow, /^  workflow_dispatch:/mu)
  assert.doesNotMatch(workflow, /^  pull_request(?:_target)?:/mu)
  assert.doesNotMatch(workflow, /workflow_run:|createCheckRun|statuses\.create/u)
  assert.match(existingWorkflow, /^  pull_request:/mu)
  assert.match(existingWorkflow, /branches: \[main\]/u)
  assert.doesNotMatch(existingWorkflow, /paired-native-contract-attestation/u)
})

it('requires exact reviewed PR heads and explicit operator review', async () => {
  const workflow = await readWorkflow()
  for (const input of [
    'producer_pull_request:',
    'expected_producer_head_sha:',
    'clients_pull_request:',
    'expected_clients_head_sha:',
    'reviewed_both_snapshots:',
    'native_test_profile:',
  ])
    assert.ok(workflow.includes(input), `missing dispatch input ${input}`)
  assert.match(workflow, /GITHUB_REF: \$\{\{ github\.ref \}\}/u)
  assert.match(workflow, /GITHUB_WORKFLOW_REF: \$\{\{ github\.workflow_ref \}\}/u)
  assert.match(workflow, /GITHUB_TRIGGERING_ACTOR: \$\{\{ github\.triggering_actor \}\}/u)
  assert.match(workflow, /GITHUB_RUN_ATTEMPT: \$\{\{ github\.run_attempt \}\}/u)
  assert.match(workflow, /permissions:\n  contents: read\n  pull-requests: read/u)
  assert.match(workflow, /run_native_tests:[\s\S]*default: false/u)
  assert.match(
    workflow,
    /native_test_profile:[\s\S]*type: choice[\s\S]*cookie-snapshot[\s\S]*pending-turn-recovery/u,
  )
})

it('passes the selected profile to every authenticated live-pair recapture', async () => {
  const workflow = await readWorkflow()
  for (const name of [
    'Resolve and bind the reviewed pull request snapshots',
    'Recheck live identities and write exact informational evidence',
    'Bind exact artifacts and recheck live pair',
  ]) {
    const step = stepBlock(workflow, name)
    assert.match(step, /INPUT_NATIVE_TEST_PROFILE: \$\{\{ inputs\.native_test_profile \}\}/u)
  }
})

it('isolates candidate code from every authenticated identity-resolution step', async () => {
  const [workflow, isolatedRunner] = await Promise.all([
    readWorkflow(),
    readFile(new URL('../scripts/paired-exporter-runner.mjs', import.meta.url), 'utf8'),
  ])
  const candidate = jobBlock(workflow, 'candidate')
  assert.match(candidate, /needs: resolve/u)
  assert.match(candidate, /permissions:\n      contents: read\n/u)
  assert.doesNotMatch(candidate, /GITHUB_TOKEN:|github\.token|secrets\./u)
  for (const step of [
    'Pull the pinned isolated exporter runtime',
    'Fetch the exact reviewed exporter dependency bundle',
    'Verify candidate execution is isolated from runner credentials and host state',
    'Run the producer exporter in the isolated pinned runtime',
    'Stage the explicitly declared contract paths',
    'Assert extracted localization representatives',
    'Check paired candidate generated localization against staged contract',
  ])
    assert.match(candidate, new RegExp(`- name: ${step}`, 'u'))
  assert.doesNotMatch(candidate, /pnpm install|pnpm\/action-setup|npm install/u)
  assert.match(candidate, /docker pull --platform linux\/amd64/u)
  assert.match(candidate, /node:26\.12\.0-bookworm@sha256:[a-f0-9]{64}/u)
  assert.match(candidate, /run-isolated-native-exporter\.mjs/u)
  assert.match(isolatedRunner, /--cap-drop=ALL/u)
  assert.match(isolatedRunner, /--network=none/u)
  assert.match(isolatedRunner, /--read-only/u)
  assert.match(isolatedRunner, /--security-opt=no-new-privileges:true/u)
  assert.match(isolatedRunner, /--tmpfs[\s\S]*\/output:rw,noexec,nosuid,size=256m/u)
  assert.match(isolatedRunner, /--tmpfs[\s\S]*\/node_modules:rw,noexec,nosuid,size=128m/u)
  assert.match(isolatedRunner, /decodeCandidateOutput/u)
  assert.match(candidate, /fetch-paired-exporter-dependencies\.mjs/u)
  assert.doesNotMatch(candidate, /type=bind,src=\$\{EXPORTER_OUTPUT_ROOT\},dst=\/output/u)
  assert.match(jobBlock(workflow, 'resolve'), /GITHUB_TOKEN: \$\{\{ github\.token \}\}/u)
  assert.match(jobBlock(workflow, 'finalize'), /GITHUB_TOKEN: \$\{\{ github\.token \}\}/u)
  assert.match(jobBlock(workflow, 'finalize'), /needs: \[resolve, candidate\]/u)
  assert.match(jobBlock(workflow, 'finalize'), /actions\/download-artifact@[a-f0-9]{40}/u)
})

it('checks out captured immutable PR merge SHAs without persisting credentials', async () => {
  const workflow = await readWorkflow()
  for (const [name, repository, refOutput] of [
    [
      'Check out the exact producer merge snapshot as untrusted data',
      'vouchington/vouchington',
      'producer_merge_sha',
    ],
    [
      'Check out the exact clients merge snapshot as untrusted data',
      'vouchington/vouchington-clients',
      'clients_merge_sha',
    ],
  ]) {
    const step = stepBlock(workflow, name)
    assert.match(step, new RegExp(`repository: ${repository.replaceAll('/', '\\/')}`, 'u'))
    assert.match(
      step,
      new RegExp(`ref: \\$\\{\\{ needs\\.resolve\\.outputs\\.${refOutput} \\}\\}`, 'u'),
    )
    assert.match(step, /fetch-depth: 0/u)
    assert.match(step, /persist-credentials: false/u)
    assert.match(step, /allow-unsafe-pr-checkout: true/u)
  }
  assert.doesNotMatch(workflow, /ref: \$\{\{ inputs\.(?:expected_|.*_ref)/u)
})

it('keeps native test execution opt-in, isolated, and informational', async () => {
  const workflow = await readWorkflow()
  const linux = jobBlock(workflow, 'native-linux-tests')
  const macSwift = jobBlock(workflow, 'native-swift-core-macos')
  const android = jobBlock(workflow, 'native-android-build')
  const emulator = jobBlock(workflow, 'native-android-emulator')
  const finalizer = jobBlock(workflow, 'finalize-native-tests')
  for (const block of [linux, macSwift, android, emulator, finalizer])
    assert.match(block, /if: \$\{\{ inputs\.run_native_tests \}\}/u)
  assert.match(linux, /runs-on: ubuntu-latest/u)
  assert.match(macSwift, /runs-on: macos-latest/u)
  assert.match(android, /runs-on: macos-latest/u)
  assert.match(emulator, /runs-on: ubuntu-24\.04/u)
  for (const block of [linux, macSwift, android, emulator]) {
    assert.match(block, /permissions:\n      contents: read/u)
    assert.doesNotMatch(block, /GITHUB_TOKEN:|github\.token|secrets\.|actions\/cache/u)
    assert.match(block, /persist-credentials: false/u)
  }
  assert.match(linux, /client-root: clients-candidate[\s\S]*tool: swift/u)
  assert.match(linux, /client-root: clients-candidate[\s\S]*tool: dotnet/u)
  assert.match(linux, /working-directory: clients-candidate[\s\S]*mise exec swift/u)
  assert.match(linux, /working-directory: clients-candidate[\s\S]*mise exec -- dotnet test/u)
  assert.match(macSwift, /client-root: clients-candidate[\s\S]*tool: swift/u)
  assert.match(macSwift, /working-directory: clients-candidate[\s\S]*mise exec swift/u)
  assert.match(macSwift, /swift-clients\/core/u)
  assert.match(workflow, /INPUT_NATIVE_TEST_PROFILE: \$\{\{ inputs\.native_test_profile \}\}/u)
  assert.match(workflow, /NATIVE_TEST_PROFILE: \$\{\{ inputs\.native_test_profile \}\}/u)
  assert.match(workflow, /needs\.native-swift-core-macos\.result/u)
  assert.match(
    android,
    /swift test --package-path "\$CLIENTS_CANDIDATE_ROOT\/swift-clients\/apps\/android"/u,
  )
  assert.match(android, /pre-push\.sh" --phase app/u)
  const androidTestAssembly = android.indexOf(':app:assembleDebugAndroidTest')
  const testApkCopy = android.indexOf('cp "$test_apk"')
  assert.ok(androidTestAssembly > android.indexOf('--phase app'))
  assert.ok(androidTestAssembly < testApkCopy)
  assert.match(android, /HOME="\$VOUCHA_SKIP_SWIFT_HOME"/u)
  assert.match(android, /CFFIXED_USER_HOME="\$VOUCHA_SKIP_SWIFT_HOME"/u)
  assert.match(
    android,
    /GRADLE_USER_HOME: \$\{\{ runner\.temp \}\}\/skip-swift-home\/\.cache\/voucha\/gradle/u,
  )
  assert.match(android, /TMPDIR="\$RUNNER_TEMP\/swift-tmp"/u)
  assert.match(
    android,
    /ANDROID_NDK_ROOT="\$VOUCHA_SKIP_SWIFT_HOME\/Library\/org\.swift\.swiftpm\/swift-sdks\/swift-6\.4\.0-RELEASE_android\.artifactbundle\/swift-android\/android-ndk-r30"/u,
  )
  assert.match(
    android,
    /ANDROID_NDK_HOME="\$VOUCHA_SKIP_SWIFT_HOME\/Library\/org\.swift\.swiftpm\/swift-sdks\/swift-6\.4\.0-RELEASE_android\.artifactbundle\/swift-android\/android-ndk-r30"/u,
  )
  const skipPathExport = android.indexOf(
    'export PATH="$CLIENTS_CANDIDATE_ROOT/swift-clients/apps/android/.build/artifacts/skip/skip/skip.artifactbundle/bin:$PATH"',
  )
  const pinnedMiseBuild = android.indexOf(
    'mise exec -- env VOUCHA_BUILD_LOCK_COMMAND_TIMEOUT_SECONDS=600',
  )
  assert.ok(skipPathExport >= 0)
  assert.ok(skipPathExport < pinnedMiseBuild)
  assert.doesNotMatch(
    android,
    /\n\s+PATH="\$CLIENTS_CANDIDATE_ROOT\/swift-clients\/apps\/android\/.build\/artifacts\/skip\/skip\/skip\.artifactbundle\/bin:\$PATH"/u,
  )
  assert.match(android, /-p "\$CLIENTS_CANDIDATE_ROOT\/swift-clients\/apps\/android\/Android"/u)
  assert.doesNotMatch(android, /gradlew.*:app:assembleDebug(?:\s|$)/u)
  assert.match(emulator, /paired-native-android-emulator\.mjs/u)
  assert.match(
    emulator,
    /^    env:\n      ANDROID_HOME: \$\{\{ github\.workspace \}\}\/\.android-sdk\n      ANDROID_SDK_ROOT: \$\{\{ github\.workspace \}\}\/\.android-sdk$/mu,
  )
  assert.match(emulator, /libx11-6 libpulse0 libpng16-16t64 libnss3 libexpat1/u)
  assert.match(emulator, /libdrm2 libxi6 libxext6 libxkbfile1/u)
  assert.match(emulator, /emulator-linux_x64-16428233\.zip/u)
  assert.match(emulator, /b08fc43d8608d2955f607f1b287beb525041e730086bdca3af152accac3af9c1/u)
  assert.match(emulator, /test "\$\(stat -c '%s' "\$archive"\)" = 349654171/u)
  assert.match(emulator, /timeout 600s unzip -q/u)
  assert.match(emulator, /stage="\$RUNNER_TEMP\/paired-pinned-emulator"/u)
  assert.match(
    emulator,
    /PINNED_EMULATOR_PATH: \$\{\{ runner\.temp \}\}\/paired-pinned-emulator\/emulator\/emulator/u,
  )
  assert.doesNotMatch(emulator, /mv "\$stage\/emulator" "\$ANDROID_HOME\/emulator"/u)
  assert.doesNotMatch(
    stepBlock(workflow, 'Download and install pinned Android emulator 37.2.12.0'),
    /\n        env:/u,
  )
  assert.doesNotMatch(emulator, /\n\s+emulator\n/u)
  assert.match(emulator, /permissions:\n      contents: read/u)
  assert.match(
    finalizer,
    /permissions:\n      actions: read\n      contents: read\n      pull-requests: read/u,
  )
  assert.match(finalizer, /finalize-native-tests/u)
  assert.match(finalizer, /GITHUB_TOKEN: \$\{\{ github\.token \}\}/u)
  assert.match(workflow, /outputs\.artifact-digest/u)
  assert.match(finalizer, /live pair/u)
  assert.doesNotMatch(workflow, /createCheckRun|statuses\.create|\bstatus: [^\n]*success/u)
})

it('keeps candidate artifacts untrusted until a clean final job rechecks live identities', async () => {
  const workflow = await readWorkflow()
  const candidateUpload = stepBlock(
    workflow,
    'Upload untrusted candidate contract data for final validation',
  )
  const finalUpload = stepBlock(workflow, 'Upload informational paired contract evidence')
  const finalize = jobBlock(workflow, 'finalize')
  assert.match(candidateUpload, /name: paired-native-candidate-\$\{\{ github\.run_id \}\}/u)
  assert.match(candidateUpload, /retention-days: 1/u)
  assert.match(
    stepBlock(workflow, 'Recheck live identities and write exact informational evidence'),
    /GITHUB_TOKEN: \$\{\{ github\.token \}\}/u,
  )
  for (const step of [
    'Verify immutable merge snapshots and trusted contract declaration',
    'Assert extracted localization representatives from untrusted data',
    'Check exact candidate generated localization against untrusted data',
  ])
    assert.match(finalize, new RegExp(`- name: ${step}`, 'u'))
  assert.match(
    finalize,
    /PRODUCER_CANDIDATE_ROOT: \$\{\{ github\.workspace \}\}\/producer-candidate/u,
  )
  assert.ok(
    finalize.indexOf('Check exact candidate generated localization against untrusted data') <
      finalize.indexOf('Recheck live identities and write exact informational evidence'),
    'trusted candidate-data checks must precede the authenticated live recheck',
  )
  assert.match(finalUpload, /name: paired-native-contract-\$\{\{ github\.run_id \}\}/u)
  assert.match(finalUpload, /retention-days: 1/u)
  assert.doesNotMatch(finalUpload, /overwrite: true/u)
  assert.doesNotMatch(
    finalize,
    /run-isolated-native-exporter|native-localization\.mts|pnpm install/u,
  )
})

it('routes the finalizer CLI command to the native evidence finalizer', async () => {
  const priorRunId = process.env.GITHUB_RUN_ID
  delete process.env.GITHUB_RUN_ID
  try {
    await assert.rejects(
      runPairedContractCommand('finalize-native-tests'),
      /workflow run ID is required/u,
    )
  } finally {
    if (priorRunId !== undefined) process.env.GITHUB_RUN_ID = priorRunId
  }
})
