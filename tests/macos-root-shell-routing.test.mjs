import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const jobBlock = (workflow, job) => {
  const start = workflow.indexOf(`  ${job}:`)
  assert.notEqual(start, -1, `missing workflow job ${job}`)
  const remainder = workflow.slice(start)
  const nextJob = remainder.search(/\n {2}[A-Za-z0-9_-]+:/u)
  return remainder.slice(0, nextJob === -1 ? remainder.length : nextJob)
}

test('macOS app smoke launches the root shell, sidebar feeds, and email sign-in', () => {
  const workflow = readFileSync(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  const macJob = jobBlock(workflow, 'build-macos-app')
  const preflight = macJob.slice(
    macJob.indexOf('- name: Verify hosted macOS GUI session'),
    macJob.indexOf('- name: Build macOS app shell'),
  )
  const launch = macJob.slice(
    macJob.indexOf('- name: Launch macOS root shell, sidebar feeds, and email sign-in'),
    macJob.indexOf('- name: Collect failed macOS launch diagnostics'),
  )
  assert.match(launch, /timeout-minutes: 15/u)

  assert.match(macJob, /runs-on: macos-latest/u)
  assert.match(macJob, /^    timeout-minutes: 40$/mu)
  assert.match(preflight, /pgrep -qx WindowServer/u)
  assert.match(preflight, /stat -f %u \/dev\/console/u)
  assert.match(preflight, /refusing to skip the macOS root-shell launch smoke/u)
  assert.doesNotMatch(preflight, /continue-on-error/u)
  assert.ok(
    macJob.indexOf('- name: Verify hosted macOS GUI session') <
      macJob.indexOf('- name: Build macOS app shell'),
  )
  assert.ok(
    macJob.indexOf('- name: Build macOS app shell') <
      macJob.indexOf('- name: Run Sentry SDK boundary tests'),
  )
  assert.ok(
    macJob.indexOf('- name: Run Sentry SDK boundary tests') <
      macJob.indexOf('- name: Launch macOS root shell, sidebar feeds, and email sign-in'),
  )
  const boundary = macJob.slice(
    macJob.indexOf('- name: Run Sentry SDK boundary tests'),
    macJob.indexOf('- name: Launch macOS root shell, sidebar feeds, and email sign-in'),
  )
  assert.match(boundary, /timeout-minutes: 10/u)
  assert.match(boundary, /-only-testing:VouchaSentryBoundaryTests/u)
  assert.match(
    boundary,
    /xcresulttool get test-results summary[\s\S]*?\.totalTestCount == 2 and \.passedTests == 2 and \.failedTests == 0 and \.skippedTests == 0/u,
  )
  assert.doesNotMatch(boundary, /VouchaLaunchSmoke\.entitlements|continue-on-error/u)
  assert.match(macJob, /generate\.sh macOS/u)
  assert.match(macJob, /-destination 'platform=macOS' build/u)
  assert.match(launch, /-destination 'platform=macOS'/u)
  assert.match(
    launch,
    /-only-testing:VouchaLaunchSmokeTests\/LaunchSmokeTests\/testLaunchRendersRootShell/u,
  )
  assert.match(
    launch,
    /-only-testing:VouchaLaunchSmokeTests\/MacOSSidebarFeedTests\/testVideoFeedSurvivesSidebarNavigation/u,
  )
  assert.match(
    launch,
    /-only-testing:VouchaLaunchSmokeTests\/MacOSEmailSignInTests\/testEmailOTPSignInReplacesTheSignedOutControl/u,
  )
  assert.match(
    launch,
    /-only-testing:VouchaLaunchSmokeTests\/MacOSSidebarPodcastTests\/testPodcastFeedSurvivesSidebarNavigation/u,
  )
  assert.match(
    launch,
    /-only-testing:VouchaLaunchSmokeTests\/MacOSSidebarPostsTests\/testPostsFeedSurvivesSidebarNavigation/u,
  )
  assert.match(launch, /VOUCHA_FILAMENTS_CONTRACT_ROOT="\$VOUCHA_FILAMENTS_CONTRACT_ROOT"/u)
  const macProject = readFileSync(
    new URL('../swift-clients/apps/macOS/project.yml', import.meta.url),
    'utf8',
  )
  assert.match(
    macProject,
    /VOUCHA_FILAMENTS_CONTRACT_ROOT: '\$\(VOUCHA_FILAMENTS_CONTRACT_ROOT\)'/u,
  )
  assert.doesNotMatch(launch, /-only-testing:.*FeedFlowTests/u)
  assert.doesNotMatch(launch, /\/SignInFlowTests\/|testNotificationSettings|testFediverse/u)
  assert.match(launch, /platform:macOS/u)
  assert.match(
    launch,
    /xcresulttool get test-results summary[\s\S]*?\.totalTestCount == 5 and \.passedTests == 5 and \.failedTests == 0 and \.skippedTests == 0/u,
  )
  assert.doesNotMatch(launch, /continue-on-error/u)
  assert.match(
    macJob,
    /-destination 'platform=macOS' build[\s\S]*CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY=""/u,
  )
  assert.match(launch, /rm -rf "\$XCODE_DERIVED_DATA\/Build\/Products\/Debug\/Voucha\.app"/u)
  assert.match(
    launch,
    /CODE_SIGN_STYLE=Manual CODE_SIGN_IDENTITY=- CODE_SIGNING_ALLOWED=YES CODE_SIGNING_REQUIRED=YES DEVELOPMENT_TEAM= PROVISIONING_PROFILE_SPECIFIER= CODE_SIGN_ENTITLEMENTS="\$PWD\/swift-clients\/apps\/macOS\/VouchaLaunchSmoke\.entitlements"/u,
  )
  assert.doesNotMatch(launch, /DEVELOPMENT_TEAM=[A-Za-z0-9]/u)
  const entitlements = readFileSync(
    new URL('../swift-clients/apps/macOS/VouchaLaunchSmoke.entitlements', import.meta.url),
    'utf8',
  )
  assert.match(entitlements, /com\.apple\.security\.app-sandbox/u)
  assert.match(entitlements, /com\.apple\.security\.network\.client/u)
  assert.doesNotMatch(entitlements, /associated-domains|applesignin|keychain-access-groups/u)
})
