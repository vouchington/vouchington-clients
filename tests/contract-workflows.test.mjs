import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { describe, it } from 'node:test'

const readWorkflow = name =>
  readFile(new URL(`../.github/workflows/${name}`, import.meta.url), 'utf8')
const readAction = name =>
  readFile(new URL(`../.github/actions/${name}/action.yml`, import.meta.url), 'utf8')

const jobBlock = (workflow, job) => {
  const start = workflow.indexOf(`  ${job}:`)
  assert.notEqual(start, -1, `missing job ${job}`)
  const remainder = workflow.slice(start)
  const nextJob = remainder.search(/\n {2}[A-Za-z0-9_-]+:/u)
  return remainder.slice(0, nextJob === -1 ? remainder.length : nextJob)
}

const preparedCandidateInputs = [
  'candidate-revision-sha: ${{ needs.verify.outputs.revision-sha }}',
  'producer-run-attempt: ${{ github.run_attempt }}',
  'producer-run-id: ${{ github.run_id }}',
]

describe('native contract workflow boundary', () => {
  it('keeps contract path ownership in the checked-in configuration', async () => {
    const config = JSON.parse(
      await readFile(new URL('../contracts/filaments.json', import.meta.url), 'utf8'),
    )
    const consumers = await Promise.all([
      readFile(new URL('../scripts/contract-artifact.mjs', import.meta.url), 'utf8'),
      readWorkflow('native-contract-tests.yml'),
    ])

    for (const path of config.paths) {
      for (const consumer of consumers) assert.equal(consumer.includes(path), false)
    }
  })

  it('uses full checkouts and pinned artifact actions', async () => {
    const files = await Promise.all([
      readWorkflow('native-contract-tests.yml'),
      readAction('prepare-native-contract'),
    ])
    const combined = files.join('\n')

    assert.doesNotMatch(combined, /sparse-checkout/u)
    assert.match(combined, /actions\/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a/u)
    assert.match(combined, /actions\/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c/u)
  })

  it('prepares the exact verified candidate for every native test job', async () => {
    const [action, workflow] = await Promise.all([
      readAction('prepare-native-contract'),
      readWorkflow('native-contract-tests.yml'),
    ])

    for (const input of ['candidate-revision-sha', 'producer-run-id', 'producer-run-attempt'])
      assert.match(action, new RegExp(`inputs\\.${input}`, 'u'))
    for (const expectation of [
      /fetch-depth: 0/u,
      /ref: \$\{\{ inputs\.candidate-revision-sha \}\}/u,
      /path: candidate-clients/u,
      /persist-credentials: false/u,
      /run-id: \$\{\{ inputs\.producer-run-id \}\}/u,
      /native-contract-\$\{\{ inputs\.producer-run-id \}\}-\$\{\{ inputs\.producer-run-attempt \}\}/u,
      /EXPECTED_REVISION_SHA: \$\{\{ inputs\.candidate-revision-sha \}\}/u,
      /--expected-revision-sha "\$EXPECTED_REVISION_SHA"/u,
    ])
      assert.match(action, expectation)

    for (const input of preparedCandidateInputs) assert.equal(workflow.split(input).length - 1, 11)
    assert.equal(workflow.split('ref: ${{ github.sha }}').length - 1, 0)
    assert.equal(workflow.split('ref: ${{ steps.event.outputs.trusted_ref }}').length - 1, 1)
    assert.equal(
      workflow.split('ref: ${{ github.event.pull_request.base.sha || github.sha }}').length - 1,
      12,
    )
  })

  it('runs portable .NET tests once on Linux and MAUI tests on macOS', async () => {
    const [action, workflow] = await Promise.all([
      readAction('prepare-native-contract'),
      readWorkflow('native-contract-tests.yml'),
    ])
    const portable = jobBlock(workflow, 'dotnet-portable')
    const maui = jobBlock(workflow, 'dotnet-maui')

    assert.match(portable, /^    name: \.NET portable$/mu)
    assert.match(portable, /runs-on: ubuntu-latest/u)
    assert.doesNotMatch(portable, /matrix:/u)
    assert.doesNotMatch(portable, /macos/u)
    assert.doesNotMatch(portable, /matrix\.os/u)
    assert.match(maui, /runs-on: macos-latest/u)
    assert.match(workflow, /dotnet test Voucha\.DotNet\.sln[\s\S]*XPlat Code Coverage/u)
    assert.match(workflow, /coverage\.info[\s\S]*TestResults\/core\/lcov\.info/u)
    assert.match(workflow, /npx --yes pnpm@11\.13\.1 run coverage:dotnet-core/u)
    for (const command of [
      /restore-locks\.sh verify/u,
      /dotnet build dotnet-clients\/tests\/Voucha\.Client\.App\.Tests\/Voucha\.Client\.App\.Tests\.csproj/u,
      /dotnet test dotnet-clients\/tests\/Voucha\.Client\.App\.Tests\/Voucha\.Client\.App\.Tests\.csproj/u,
      /dotnet build dotnet-clients\/src\/Voucha\.Client\.App\/Voucha\.Client\.App\.csproj[\s\S]*--framework net10\.0-maccatalyst/u,
    ])
      assert.match(workflow, command)
    assert.match(workflow, /name: Select compatible Xcode[\s\S]*id: xcode[\s\S]*compatible=false/u)
    assert.match(
      workflow,
      /xcrun --sdk macosx --show-sdk-path[\s\S]*xcrun --sdk macosx --find actool[\s\S]*\[ -d "\$sdk_path" \][\s\S]*\[ -x "\$actool" \]/u,
    )
    assert.match(
      workflow,
      /name: Build MAUI Mac Catalyst app\n\s+if: steps\.xcode\.outputs\.compatible == 'true'/u,
    )
    assert.match(
      workflow,
      /name: Restore the shared MAUI project-reference graph\n\s+if: steps\.xcode\.outputs\.compatible == 'true'[\s\S]*?dotnet restore dotnet-clients\/src\/Voucha\.Client\.App\/Voucha\.Client\.App\.csproj \\\n\s+-p:Configuration=Release -p:TargetFramework=net10\.0-maccatalyst \\\n\s+-p:RuntimeIdentifier=\$\{\{ steps\.rid\.outputs\.runtime_identifier \}\} --locked-mode\n\s+dotnet restore dotnet-clients\/src\/Voucha\.Client\.Core\/Voucha\.Client\.Core\.csproj \\\n\s+-p:Configuration=Release -p:TargetFramework=net10\.0 --locked-mode\n\s+- name: Build MAUI Mac Catalyst app/u,
    )
    assert.ok(
      workflow.indexOf('name: Test rendered MAUI pages') <
        workflow.indexOf('name: Select compatible Xcode'),
    )
    assert.doesNotMatch(workflow, /clean-workspace/u)
    assert.doesNotMatch(action, /clean-workspace/u)
  })

  it('installs each candidate .NET SDK at its exact global.json version', async () => {
    const workflow = await readWorkflow('native-contract-tests.yml')

    assert.equal(workflow.split('name: Read exact .NET SDK version').length - 1, 2)
    assert.equal(workflow.split('id: dotnet-sdk').length - 1, 2)
    assert.equal(workflow.split('working-directory: candidate-clients').length - 1 >= 2, true)
    assert.equal(
      workflow.split(
        'jq -er \'.sdk.version | select(type == "string" and test("^[0-9]+\\\\.[0-9]+\\\\.[0-9]+$"))\' global.json',
      ).length - 1,
      2,
    )
    assert.equal(
      workflow.split('dotnet-version: ${{ steps.dotnet-sdk.outputs.version }}').length - 1,
      2,
    )
    assert.doesNotMatch(workflow, /global-json-file: candidate-clients\/global\.json/u)
    assert.doesNotMatch(workflow, /dotnet-version:\s*["']?\d/u)
  })

  it('runs the complete standalone Swift consumer matrix without trusted coverage transport', async () => {
    const [action, workflow, validation] = await Promise.all([
      readAction('prepare-native-contract'),
      readWorkflow('native-contract-tests.yml'),
      readWorkflow('validate.yml'),
    ])
    const candidateJobs = workflow.slice(workflow.indexOf('  verify:'))

    for (const job of [
      'periphery-swift-core:',
      'periphery-swift-ui:',
      'test-swift-core-linux:',
      'test-swift-core:',
      'test-swift-android:',
      'test-swift-ui:',
      'swift-patch-coverage:',
      'build-android-core:',
      'build-macos-app:',
    ])
      assert.match(workflow, new RegExp(`^  ${job}`, 'mu'))
    assert.match(validation, /"\$SWIFTFORMAT_IMAGE" swift-clients\/ --lint --quiet --verbose$/mu)
    assert.doesNotMatch(validation, /swift-clients\/ --lint --verbose/u)
    assert.match(validation, /"\$SWIFTLINT_IMAGE" --strict --cache-path/u)
    assert.match(
      validation,
      /"\$SWIFTLINT_IMAGE" --strict --config \.swiftlint-tests\.yml --cache-path/u,
    )
    assert.match(workflow, /periphery scan --strict/u)
    assert.match(workflow, /--enable-code-coverage/u)
    assert.match(
      workflow,
      /write-lcov\.sh swift-clients\/core VouchaCorePackageTests coverage\/core\/lcov\.info/u,
    )
    assert.match(
      workflow,
      /write-lcov\.sh swift-clients\/ui VouchaUIPackageTests coverage\/ui\/lcov\.info/u,
    )
    assert.match(workflow, /npx --yes pnpm@11\.13\.1 run coverage:swift/u)
    assert.match(
      workflow,
      /android-actions\/setup-android@40fd30fb8d7440372e1316f5d1809ec01dcd3699/u,
    )
    assert.match(
      workflow,
      /swiftly_sha256="fade009739a84f18ee30e524793f927019fc9c2e16b2ad958da50d3f9ff7a7f8"/u,
    )
    assert.match(workflow, /export SWIFTLY_HOME_DIR="\$skip_swift_home\/\.swiftly"/u)
    assert.match(workflow, /materialize-skip-sdk\.sh/u)
    assert.match(workflow, /VOUCHA_SKIP_ANDROID_HOST_SWIFT_TEST: ["']1["']/u)
    assert.match(workflow, /Record swift test start marker/u)
    assert.match(workflow, /Collect xctest crash reports[\s\S]*if: \$\{\{ failure\(\) \}\}/u)
    assert.match(workflow, /Upload core LCOV[\s\S]*swift-core-lcov/u)
    assert.match(workflow, /Upload UI LCOV[\s\S]*swift-ui-lcov/u)
    assert.match(workflow, /Upload xctest crash reports[\s\S]*swift-ui-crash-reports/u)
    assert.match(
      workflow,
      /swift:6\.3\.3-noble@sha256:66520bcba471018a34fd54ba09be97ba4abebd950a96ff5cb8c2bf50a2d33259/u,
    )
    assert.match(workflow, /xcodegen-\$XCODEGEN_VERSION\.zip/u)
    assert.match(
      workflow,
      /test "\$\("\$xcodegen_bin" --version\)" = "Version: \$\{XCODEGEN_VERSION\}"/u,
    )
    assert.match(workflow, /rm -rf "\$XCODE_DERIVED_DATA" "\$SWIFT_PACKAGE_CLONES"/u)
    assert.match(workflow, /#6705[\s\S]*iOS[\s\S]*Simulator destination/u)
    assert.doesNotMatch(candidateJobs, /coverage-transport|s3_transport|secrets\./u)
    assert.match(action, /VOUCHA_FILAMENTS_CONTRACT_ROOT=\$RUNNER_TEMP\/native-contract/u)
    assert.match(validation, /npx --yes pnpm@11\.13\.1 install --frozen-lockfile/u)
    assert.equal(
      workflow.split('candidate-revision-sha: ${{ needs.verify.outputs.revision-sha }}').length - 1,
      11,
    )
  })

  it('runs Swift patch coverage on Linux without compiling Swift', async () => {
    const workflow = await readWorkflow('native-contract-tests.yml')
    const coverageJob = jobBlock(workflow, 'swift-patch-coverage')

    assert.match(coverageJob, /runs-on: ubuntu-latest/u)
    assert.equal(coverageJob.split('npx --yes pnpm@11.13.1 run coverage:swift').length - 1, 1)
    assert.doesNotMatch(coverageJob, /swift (?:build|test)/u)
  })

  it('uses the exact native runner labels for Swift jobs', async () => {
    const workflow = await readWorkflow('native-contract-tests.yml')
    for (const job of [
      'periphery-swift-core',
      'periphery-swift-ui',
      'test-swift-core',
      'test-swift-android',
      'test-swift-ui',
      'build-macos-app',
    ])
      assert.match(jobBlock(workflow, job), /runs-on: macos-latest/u)
    for (const job of ['test-swift-core-linux', 'build-android-core'])
      assert.match(jobBlock(workflow, job), /runs-on: ubuntu-latest/u)
    assert.match(
      jobBlock(workflow, 'test-swift-core-linux'),
      /swift test --package-path swift-clients\/core/u,
    )
    assert.match(
      jobBlock(workflow, 'test-swift-core-linux'),
      /swift test --package-path swift-clients\/test-support/u,
    )
    assert.match(jobBlock(workflow, 'test-swift-core-linux'), /--user "\$\(id -u\):\$\(id -g\)"/u)
    assert.match(
      jobBlock(workflow, 'test-swift-core-linux'),
      /--build-path \/tmp\/voucha-core-build/u,
    )
    assert.doesNotMatch(workflow, /clean: false/u)
  })

  it('runs Swift lint on Linux without compiling Swift', async () => {
    const workflow = await readWorkflow('validate.yml')
    const lintJob = jobBlock(workflow, 'swift-lint')

    assert.match(lintJob, /runs-on: ubuntu-latest/u)
    assert.doesNotMatch(lintJob, /DEVELOPER_DIR|setup-swift-native/u)
    assert.match(
      lintJob,
      /SWIFTFORMAT_IMAGE: ghcr\.io\/nicklockwood\/swiftformat:[^\s]+@sha256:[0-9a-f]{64}/u,
    )
    assert.match(lintJob, /SWIFTLINT_IMAGE: ghcr\.io\/realm\/swiftlint:[^\s]+@sha256:[0-9a-f]{64}/u)
    assert.equal(lintJob.split('docker run').length - 1, 3)
    assert.equal(lintJob.split('"$SWIFTFORMAT_IMAGE"').length - 1, 1)
    assert.equal(lintJob.split('"$SWIFTLINT_IMAGE"').length - 1, 2)
    assert.match(
      lintJob,
      /"\$SWIFTFORMAT_IMAGE" swift-clients\/ --lint --quiet --verbose/u,
      'static Linux SwiftFormat needs quiet verbose execution to avoid swiftlang/swift#77841',
    )
    assert.doesNotMatch(lintJob, /swift (?:build|test)/u)
  })

  it('uses the repository SDK policy in required .NET validation', async () => {
    const validation = await readWorkflow('validate.yml')

    assert.match(validation, /global-json-file: global\.json/u)
    assert.doesNotMatch(validation, /dotnet-version: 10\.0\.x/u)
    assert.doesNotMatch(validation, /DOTNET_INSTALL_DIR/u)
    assert.doesNotMatch(validation, /mise_dir:/u)
  })

  it('checks out the public Vouchington producer without a deploy key', async () => {
    const workflow = await readWorkflow('native-contract-tests.yml')
    const action = await readAction('prepare-native-contract')

    assert.match(workflow, /pull_request:/u)
    assert.doesNotMatch(workflow, /pull_request_target:/u)
    assert.doesNotMatch(jobBlock(workflow, 'produce'), /pull_request\.head\.repo\.full_name/u)
    assert.doesNotMatch(workflow, /FILAMENTS_DEPLOY_KEY/u)
    assert.doesNotMatch(workflow, /ssh-key:/u)
    assert.match(workflow, /candidate-clients\/contracts\/filaments\.json/u)
    assert.match(
      workflow,
      /  produce:[\s\S]*?repository: vouchington\/vouchington[\s\S]*?  verify:/u,
    )
    assert.equal(workflow.split('--contract-repository vouchington/vouchington').length - 1, 1)
    assert.equal(
      `${workflow}\n${action}`.split('--expected-contract-repository vouchington/vouchington')
        .length - 1,
      2,
    )
    assert.doesNotMatch(action, /secrets\./u)
    assert.doesNotMatch(workflow, /workflow_run:/u)
  })

  it('stages the trusted contract before parity and artifact creation', async () => {
    const producer = await readWorkflow('native-contract-tests.yml')

    assert.match(producer, /name: Install trusted Vouchington exporter dependencies/u)
    assert.match(
      producer,
      /name: Install trusted Vouchington exporter dependencies\n\s+working-directory: filaments\n\s+run: npx --yes pnpm@11\.13\.1 install --frozen-lockfile/u,
    )
    assert.match(producer, /name: Stage trusted native contract/u)
    assert.match(producer, /scripts\/stage-native-contract\.mjs/u)
    assert.match(producer, /--filaments-root "\$FILAMENTS_ROOT"/u)
    assert.match(producer, /--output-root "\$CONTRACT_STAGE_ROOT"/u)
    assert.match(producer, /--consumer-root "\$CANDIDATE_ROOT"/u)
    assert.match(producer, /name: Assert extracted localization representatives/u)
    assert.match(producer, /scripts\/assert-extracted-localization\.mjs/u)
    assert.match(
      producer,
      /VOUCHA_FILAMENTS_CONTRACT_ROOT: \$\{\{ runner\.temp \}\}\/native-contract-stage-\$\{\{ github\.run_id \}\}-\$\{\{ github\.run_attempt \}\}/u,
    )
    assert.equal(
      producer.indexOf('name: Install trusted Vouchington exporter dependencies') <
        producer.indexOf('name: Stage trusted native contract'),
      true,
    )
    assert.equal(
      producer.indexOf('name: Stage trusted native contract') <
        producer.indexOf('name: Assert extracted localization representatives'),
      true,
    )
    assert.equal(
      producer.indexOf('name: Assert extracted localization representatives') <
        producer.indexOf('name: Assert candidate generated localization parity'),
      true,
    )
  })

  it('preserves exact run identities and exposes one aggregate Tests gate', async () => {
    const workflow = await readWorkflow('native-contract-tests.yml')

    assert.match(
      workflow,
      /run-name: >-\n\s+Native contract tests for \$\{\{ github\.event\.pull_request\.number.*format\('main at \{0\}', github\.sha\) \}\}/u,
    )
    assert.match(workflow, /retention-days: 1/u)
    assert.match(workflow, /run-id: \$\{\{ github\.run_id \}\}/u)
    assert.match(workflow, /--expected-producer-run-attempt/u)
    assert.match(workflow, /  tests:\n\s+name: Tests\n\s+if: always\(\)/u)
    assert.doesNotMatch(workflow, /pull_request\.head\.repo\.full_name/u)
    assert.doesNotMatch(workflow, /Filaments contract parity|check-runs/u)
  })

  it('reports DTO fixture field drift in one advisory check outside the Tests gate', async () => {
    const workflow = await readWorkflow('native-contract-tests.yml')
    const dotnetJob = jobBlock(workflow, 'dotnet-portable')
    const swiftJob = jobBlock(workflow, 'test-swift-core')
    const parityJob = jobBlock(workflow, 'native-dto-fixture-parity')
    const testsJob = jobBlock(workflow, 'tests')

    assert.match(
      dotnetJob,
      /--filter "FullyQualifiedName!~ApiFixtureCoverageTests\.FixtureFieldsRoundTripThroughTheDto"/u,
    )
    assert.match(
      dotnetJob,
      /name: Test \.NET DTO fixture parity separately[\s\S]*if: always\(\)[\s\S]*continue-on-error: true/u,
    )
    assert.match(
      swiftJob,
      /--skip 'VouchaCoreTests\.ApiFixtureCoverageTests\/testRegisteredFixturesRoundTripThroughTheirDTO'/u,
    )
    assert.match(
      swiftJob,
      /name: Test Swift DTO fixture parity separately[\s\S]*if: always\(\)[\s\S]*continue-on-error: true/u,
    )
    assert.ok(
      swiftJob.indexOf('name: Test Swift DTO fixture parity separately') >
        swiftJob.indexOf('name: Upload core LCOV'),
      'Swift parity must not rebuild the test bundle before coverage export',
    )
    assert.match(parityJob, /name: Native DTO fixture parity \(advisory\)/u)
    assert.match(parityJob, /Report missing native DTO fields/u)
    assert.doesNotMatch(testsJob, /native-dto-fixture-parity/u)
  })
})
