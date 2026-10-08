import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const read = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8')
const jobBlock = (workflow, name) => {
  const start = workflow.indexOf(`  ${name}:`)
  if (start < 0) throw new Error(`Missing workflow job ${name}`)
  const tail = workflow.slice(start)
  const next = tail.search(/\n  [A-Za-z0-9_-]+:/u)
  return next < 0 ? tail : tail.slice(0, next)
}

test('mise, global.json, and MAUI CI select the Xcode-compatible .NET 10 SDK set', () => {
  const mise = read('.mise.toml')
  const global = JSON.parse(read('global.json'))
  const workflow = read('.github/workflows/native-contract-tests.yml')

  assert.equal(global.sdk.version, '10.0.401')
  assert.equal(global.sdk.workloadVersion, '10.0.401')
  assert.match(mise, /^dotnet = "10\.0\.401"$/mu)
  const workloadInstall = workflow.match(
    /dotnet workload install maui-maccatalyst(?:\n[ \t]+[^\n]+)*/u,
  )?.[0]
  assert.match(workloadInstall ?? '', /--configfile dotnet-clients\/NuGet\.config/u)
  assert.doesNotMatch(workloadInstall ?? '', /--version/u)
  assert.match(workflow, /select-xcode\.sh 26\.6/u)
  assert.doesNotMatch(workflow, /actions\/setup-dotnet@/u)
  assert.match(
    workflow,
    /setup-mise-toolchain[\s\S]*?client-root: candidate-clients[\s\S]*?tool: dotnet/u,
  )
  for (const path of [
    '.github/workflows/validate.yml',
    '.github/workflows/repair-dependabot-dotnet-locks.yml',
  ]) {
    const dotnetWorkflow = read(path)
    assert.doesNotMatch(dotnetWorkflow, /actions\/setup-dotnet@/u)
    assert.match(dotnetWorkflow, /setup-mise-toolchain[\s\S]*?client-root: \.\s+tool: dotnet/u)
  }
})

test('Swift 6.4.0 and its Android SDK and NDK archives stay aligned', () => {
  const mise = read('.mise.toml')
  const images = read('dev/linux-native-images.sh')
  const workflow = read('.github/workflows/native-contract-tests.yml')
  const linuxAndroid = read('swift-clients/tooling/build-android-core.sh')
  const macAndroid = read('swift-clients/apps/android/tooling/materialize-skip-sdk.sh')

  assert.match(mise, /^swift = "6\.4\.0"$/mu)
  assert.match(images, /swift:6\.4\.0-noble@sha256:[0-9a-f]{64}/u)
  assert.equal((workflow.match(/swift:6\.4\.0-noble@sha256:[0-9a-f]{64}/gu) ?? []).length, 2)
  assert.match(linuxAndroid, /SWIFT_ANDROID_SDK_VERSION="6\.4\.0"/u)
  assert.match(macAndroid, /SWIFT_VERSION="6\.4\.0"/u)
  assert.match(linuxAndroid, /ANDROID_NDK_VERSION="r30"/u)
  assert.match(
    linuxAndroid,
    /export ANDROID_NDK_HOME="\$ANDROID_NDK_PATH"[\s\S]*?setup-android-sdk\.sh[\s\S]*?swift build/u,
  )
  assert.match(macAndroid, /ANDROID_NDK_VERSION="r30"/u)
  const androidJob = workflow.slice(
    workflow.indexOf('  test-swift-android:'),
    workflow.indexOf('  test-swift-ui:'),
  )
  assert.match(
    androidJob,
    /setup-mise-toolchain[\s\S]*?client-root: candidate-clients[\s\S]*?tool: swift/u,
  )
  assert.match(androidJob, /mise exec -- swift test/u)
  assert.doesNotMatch(androidJob, /swiftly|Provision pinned job-scoped Swiftly/u)
  assert.match(macAndroid, /mise which swift/u)
  assert.match(macAndroid, /resolve-mise-swift-toolchain\.sh/u)
  const coreJob = jobBlock(workflow, 'test-swift-core')
  const uiJob = jobBlock(workflow, 'test-swift-ui')
  assert.match(coreJob, /setup-mise-toolchain[\s\S]*?tool: swift/u)
  assert.match(coreJob, /mise exec -- swift test/u)
  assert.doesNotMatch(coreJob, /VOUCHA_PERIPHERY_SWIFT_TOOLCHAIN: xcode/u)
  assert.doesNotMatch(uiJob, /setup-mise-toolchain/u)
  assert.match(uiJob, /select-xcode\.sh 26\.6[\s\S]*?xcrun swift test/u)
  const qualityAction = read('.github/actions/setup-swift-native/action.yml')
  assert.doesNotMatch(qualityAction, /mise install swift/u)
  assert.match(qualityAction, /mise install aqua:peripheryapp\/periphery@3\.7\.4/u)
  const setupAction = read('.github/actions/setup-mise-toolchain/action.yml')
  assert.match(setupAction, /case "\$MISE_TOOL" in[\s\S]*?dotnet\|swift/u)
  assert.match(setupAction, /mise_data_dir="\$RUNNER_TEMP\/mise-data"/u)
  assert.match(setupAction, /mise ls --current --json[\s\S]*?mise exec -- swift --version/u)
  assert.doesNotMatch(setupAction, /resolve-mise-swift-toolchain\.sh/u)
})

test('Skip package and verified materializer use the same Swift-compatible release', () => {
  const manifest = read('swift-clients/apps/android/Package.swift')
  const lock = JSON.parse(read('swift-clients/apps/android/Package.resolved'))
  const materializer = read('swift-clients/apps/android/tooling/materialize-skip-sdk.sh')

  const manifestVersion = manifest.match(/skip\.git", exact: "([0-9.]+)"/u)?.[1]
  const lockedVersion = lock.pins.find(pin => pin.identity === 'skip')?.state.version
  assert.equal(manifestVersion, '1.9.13')
  assert.equal(lockedVersion, manifestVersion)
  assert.match(materializer, /SKIP_VERSION="1\.9\.13"/u)
  assert.match(materializer, /resolve-mise-swift-toolchain\.sh/u)
  assert.doesNotMatch(materializer, /swiftly/u)
})

test('portable Swift uses mise while Apple UI package operations use selected Xcode Swift', () => {
  const harness = read('swift-clients/tooling/harness.sh')
  const workflow = read('.github/workflows/native-contract-tests.yml')
  const uiJob = jobBlock(workflow, 'test-swift-ui')
  const uiPeripheryJob = jobBlock(workflow, 'periphery-swift-ui')
  const macAppJob = workflow.slice(
    workflow.indexOf('  build-macos-app:'),
    workflow.indexOf('\n  # iOS simulator smoke', workflow.indexOf('  build-macos-app:')),
  )

  assert.match(harness, /SWIFT_COMMAND=\(mise exec -- swift\)/u)
  assert.match(harness, /XCODE_SWIFT_COMMAND=\(xcrun swift\)/u)
  assert.match(workflow, /mise exec -- swift test --package-path swift-clients\/core/u)
  assert.match(uiJob, /select-xcode\.sh 26\.6/u)
  assert.match(uiJob, /xcrun swift test --package-path swift-clients\/ui/u)
  assert.match(uiPeripheryJob, /select-xcode\.sh 26\.6/u)
  assert.match(uiPeripheryJob, /VOUCHA_PERIPHERY_SWIFT_TOOLCHAIN: xcode/u)
  assert.match(macAppJob, /xcodebuild -project/u)
  assert.doesNotMatch(macAppJob, /mise exec -- swift/u)
})

test('Periphery scans the index store produced by its selected Swift compiler', () => {
  const scan = read('swift-clients/tooling/periphery-scan.sh')
  const harness = read('swift-clients/tooling/harness.sh')
  const workflow = read('.github/workflows/native-contract-tests.yml')
  const corePeripheryJob = jobBlock(workflow, 'periphery-swift-core')
  const uiPeripheryJob = jobBlock(workflow, 'periphery-swift-ui')

  assert.match(scan, /--build-path \.build\/periphery-index/u)
  assert.match(scan, /--enable-index-store/u)
  assert.match(scan, /--build-tests/u)
  assert.match(scan, /--force-resolved-versions/u)
  assert.match(scan, /--disable-dependency-cache/u)
  assert.match(scan, /--show-bin-path/u)
  assert.match(scan, /INDEX_STORE="\$BIN_PATH\/index\/store"/u)
  assert.match(scan, /outside the scoped build path/u)
  assert.match(scan, /periphery scan --strict --skip-build --index-store-path "\$INDEX_STORE"/u)
  assert.match(scan, /mise\)\s+SWIFT_COMMAND=\(mise exec -- swift\)/u)
  assert.match(scan, /xcode\)\s+SWIFT_COMMAND=\(xcrun swift\)/u)
  assert.match(harness, /periphery-scan\.sh" core/u)
  assert.match(harness, /periphery-scan\.sh" ui/u)
  assert.doesNotMatch(harness, /\.build\/debug\/index\/store/u)
  assert.match(corePeripheryJob, /tool: swift/u)
  assert.doesNotMatch(corePeripheryJob, /VOUCHA_PERIPHERY_SWIFT_TOOLCHAIN: xcode/u)
  assert.match(uiPeripheryJob, /VOUCHA_PERIPHERY_SWIFT_TOOLCHAIN: xcode/u)
  assert.doesNotMatch(uiPeripheryJob, /setup-mise-toolchain/u)
  assert.match(corePeripheryJob, /periphery-scan\.sh core/u)
  assert.match(uiPeripheryJob, /periphery-scan\.sh ui/u)
})

test('Linux mise jobs install the Swift runtime libraries they require', () => {
  const action = read('.github/actions/setup-mise-toolchain/action.yml')
  const validation = read('.github/workflows/validate.yml')
  assert.match(
    action,
    /if: runner\.os == 'Linux'[\s\S]*?apt-get install --yes --no-install-recommends libncurses6[\s\S]*?mise-action/u,
  )
  for (const job of ['tooling-lint', 'gitleaks', 'swift-lint']) {
    const block = jobBlock(validation, job)
    assert.match(block, /apt-get install --yes --no-install-recommends libncurses6/u)
    const aptIndex = block.indexOf('apt-get install --yes --no-install-recommends libncurses6')
    const miseExecIndex = block.indexOf('mise exec')
    if (miseExecIndex >= 0)
      assert.ok(aptIndex < miseExecIndex, `${job} installs libs before mise exec`)
  }
})
