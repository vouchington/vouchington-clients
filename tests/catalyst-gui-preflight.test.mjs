import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import {
  cpSync,
  existsSync,
  mkdtempSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
import { notificationLocalizationFile } from '../scripts/catalyst-notification-app-evidence.mjs'
import { jobBlock as readJobBlock } from './workflow-job-block.mjs'

const jobBlock = (workflow, job) => readJobBlock(workflow, job, `missing workflow job ${job}`)

const workflow = readFileSync(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)
const sessionScript = readFileSync(
  new URL('../dotnet-clients/tooling/catalyst-gui-preflight.mjs', import.meta.url),
  'utf8',
)
const preflightSource = readFileSync(
  new URL('../dotnet-clients/src/Voucha.Client.App/CatalystGuiPreflight.cs', import.meta.url),
  'utf8',
)
const appSource = readFileSync(
  new URL('../dotnet-clients/src/Voucha.Client.App/App.xaml.cs', import.meta.url),
  'utf8',
)
const handlerSource = readFileSync(
  new URL('../dotnet-clients/src/Voucha.Client.App/CatalystGuiFixtureHandler.cs', import.meta.url),
  'utf8',
)
const authSource = readFileSync(
  new URL('../dotnet-clients/src/Voucha.Client.App/MauiProgram.Auth.cs', import.meta.url),
  'utf8',
)
const notificationScript = readFileSync(
  new URL('../dotnet-clients/tooling/catalyst-gui-notification-session.mjs', import.meta.url),
  'utf8',
)

describe('Mac Catalyst GUI preflight', () => {
  it('requires root-shell and notification Debug Mac2 sessions inside a 15-minute job', () => {
    const job = jobBlock(workflow, 'dotnet-catalyst-gui')
    const maui = jobBlock(workflow, 'dotnet-maui')
    const needs = jobBlock(workflow, 'tests')

    assert.match(job, /name: Mac Catalyst GUI preflight/u)
    assert.match(job, /runs-on: macos-latest/u)
    assert.match(job, /timeout-minutes: 15/u)
    assert.match(job, /--configuration Debug --framework net10\.0-maccatalyst/u)
    assert.match(job, /name 'Voucha\.app'/u)
    assert.match(job, /name 'Voucha\.app' \| wc -l \| tr -d ' '\)" -eq 1/u)
    assert.match(job, /Restore NuGet packages\n\s+uses: actions\/cache\/restore@/u)
    assert.doesNotMatch(job, /Save NuGet packages/u)
    assert.match(
      job,
      /Restore Appium Mac2\n\s+id: catalyst-appium-cache\n\s+uses: actions\/cache\/restore@/u,
    )
    assert.doesNotMatch(job, /uses: actions\/cache@/u)
    assert.match(
      job,
      /Save Appium Mac2 from main\n\s+if: \$\{\{ github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.catalyst-appium-cache\.outputs\.cache-hit != 'true' \}\}/u,
    )
    assert.match(job, /catalyst-gui-preflight\.mjs --stage-fixture/u)
    assert.match(job, /-p:CodesignEntitlements=/u)
    assert.match(job, /catalyst-gui-preflight\.mjs --root-session/u)
    assert.match(job, /root-result\.json/u)
    assert.match(job, /catalyst-gui-preflight\.mjs --session/u)
    assert.match(sessionScript, /--ui-testing-launch-smoke/u)
    assert.match(job, /pgrep -qx WindowServer/u)
    assert.match(job, /refusing to skip the Mac Catalyst GUI preflight/u)
    assert.match(
      job,
      /node scripts\/catalyst-notification-evidence\.mjs "\$CATALYST_GUI_RESULT" "\$CATALYST_NATIVE_REVISION"/u,
    )
    assert.match(
      job,
      /Upload Catalyst control and dispatch evidence\n\s+if: \$\{\{ success\(\) \}\}/u,
    )
    assert.match(job, /result\.json\.app\.json\n\s+retention-days: 7\n\s+if-no-files-found: error/u)
    assert.equal(
      job.match(/jq -e '\.total == 3 and \.passed == 3 and \.failed == 0 and \.skipped == 0'/gu)
        ?.length,
      1,
      'root session requires its independent non-skipping runtime result',
    )
    assert.match(
      job,
      /CATALYST_GUI_RESULT="\$diagnostics\/root-result\.json"\n\s+node dotnet-clients\/tooling\/catalyst-gui-preflight\.mjs --root-session\n\s+jq[^\n]+\n\s+export CATALYST_GUI_RESULT="\$diagnostics\/result\.json"\n\s+export CATALYST_NATIVE_REVISION="\$\{\{ needs\.verify\.outputs\.revision-sha \}\}"\n\s+node dotnet-clients\/tooling\/catalyst-gui-preflight\.mjs --session/u,
    )

    assert.match(needs, /^      - dotnet-catalyst-gui$/mu)
    assert.match(maui, /timeout-minutes: 30/u)
    assert.match(maui, /--configuration Release --framework net10\.0-maccatalyst/u)
    assert.doesNotMatch(maui, /appium|catalyst-gui-preflight/u)
    assert.match(preflightSource, /^#if DEBUG\n/u)
    assert.match(preflightSource, /swift\.my\.identity\.default/u)
    assert.match(preflightSource, /native\.my\.email-preferences\.default/u)
    assert.match(preflightSource, /CreateProductionWindow/u)
    assert.doesNotMatch(preflightSource, /CreateRoot/u)
    assert.match(appSource, /CatalystGuiPreflight\.IsActive\(\)/u)
    assert.match(appSource, /CatalystGuiPreflight\.CreateProductionWindow\(serviceProvider\)/u)
    assert.match(handlerSource, /^#if DEBUG\n/u)
    assert.match(handlerSource, /\/api\/v1\/my\/email-preferences/u)
    assert.match(
      authSource,
      /#if DEBUG\n\s+transport = CatalystGuiFixtureHandler\.Wrap\(transport\);/u,
    )
    assert.match(
      authSource,
      /#if DEBUG\n\s+return CatalystGuiSessionCookiePersistence\.Select\(CatalystGuiPreflight\.IsActive\(\), Production\);\n#else\n\s+return Production\(\);/u,
    )
    assert.match(sessionScript, /native\.my\.email-preferences\.default/u)
    assert.match(sessionScript, /notificationEvidence\(checks, nativeRevision, commit\)/u)
    assert.match(
      job,
      /CATALYST_NATIVE_REVISION="\$\{\{ needs\.verify\.outputs\.revision-sha \}\}"/u,
    )
    assert.match(sessionScript, /total: 3, passed: 3, failed: 0, skipped: 0/u)
    assert.match(sessionScript, /--ui-testing-root-shell-smoke/u)
    assert.match(
      sessionScript,
      /if \(rootShell\) await assertRootShellControls\(request, sessionId\)\n\s+else \{\n\s+const inputs = notificationInputs\(process\.env\.VOUCHA_CONTRACT_ROOT\)\n\s+checks = await assertNotificationControls\(request, sessionId, \{/u,
    )
    assert.match(preflightSource, /CatalystGuiLaunchOptions/u)
    assert.match(notificationScript, /toggle\('engagement'\)/u)
    assert.match(notificationScript, /toggle\('moderation'\)/u)
    assert.equal(job.match(/timeout-minutes:/gu)?.length, 10)
    assert.match(job, /timeout-minutes: 6\n/u)
    assert.match(job, /timeout-minutes: 3\n/u)
    assert.match(job, /timeout-minutes: 2\n/u)
    assert.match(job, /timeout-minutes: 1\n/u)
  })

  it('stages identity, preferences, rotation and generated client localization', t => {
    const root = mkdtempSync(join(tmpdir(), 'catalyst-fixture-'))
    t.after(() => rmSync(root, { recursive: true, force: true }))
    const consumer = join(root, 'consumer')
    cpSync(
      new URL('../dotnet-clients/tooling/', import.meta.url),
      join(consumer, 'dotnet-clients/tooling'),
      { recursive: true },
    )
    mkdirSync(join(consumer, 'scripts'), { recursive: true })
    for (const file of [
      'catalyst-notification-evidence.mjs',
      'catalyst-notification-app-evidence.mjs',
    ]) {
      cpSync(new URL(`../scripts/${file}`, import.meta.url), join(consumer, 'scripts', file))
    }
    const fixtureRoot = join(root, 'api-fixtures', 'v1')
    mkdirSync(join(fixtureRoot, 'responses'), { recursive: true })
    writeFileSync(join(fixtureRoot, 'responses', 'swift.my.identity.default.json'), '{}\n')
    writeFileSync(
      join(fixtureRoot, 'responses', 'native.my.email-preferences.default.json'),
      '{"email_preferences":{}}\n',
    )
    const sessionBody = '{"session":{"uid":"member","session":{"uid":"member"}}}\n'
    writeFileSync(
      join(fixtureRoot, 'responses', 'native.auth.session.refresh.rotated.json'),
      sessionBody,
    )
    const sessionFixture = {
      id: 'native.auth.session.refresh.rotated',
      method: 'PATCH',
      path: '/api/v1/session',
      status: 200,
      bodyFile: 'responses/native.auth.session.refresh.rotated.json',
    }
    const identityFixture = {
      id: 'swift.my.identity.default',
      method: 'GET',
      path: '/api/v1/my/identity',
      status: 200,
      bodyFile: 'responses/swift.my.identity.default.json',
    }
    const emailFixture = {
      id: 'native.my.email-preferences.default',
      method: 'GET',
      path: '/api/v1/my/email-preferences',
      status: 200,
      bodyFile: 'responses/native.my.email-preferences.default.json',
    }
    writeFileSync(
      join(fixtureRoot, 'manifest.json'),
      JSON.stringify({ fixtures: [identityFixture, emailFixture, sessionFixture] }),
    )
    const script = join(consumer, 'dotnet-clients/tooling/catalyst-gui-preflight.mjs')
    execFileSync(process.execPath, [script, '--check-fixture'], {
      env: { ...process.env, VOUCHA_CONTRACT_ROOT: root },
    })
    assert.equal(existsSync(join(root, notificationLocalizationFile)), false)
    const resource = join(consumer, notificationLocalizationFile)
    mkdirSync(join(resource, '..'), { recursive: true })
    writeFileSync(resource, '<root>generated client localization</root>')
    const staged = join(root, 'staged')
    execFileSync(process.execPath, [script, '--stage-fixture', staged], {
      env: { ...process.env, VOUCHA_CONTRACT_ROOT: root },
      cwd: root,
    })
    assert.equal(
      existsSync(join(staged, 'api-fixtures', 'v1', 'responses', 'swift.my.identity.default.json')),
      true,
    )
    assert.equal(
      existsSync(
        join(staged, 'api-fixtures', 'v1', 'responses', 'native.my.email-preferences.default.json'),
      ),
      true,
    )
    assert.equal(
      readFileSync(join(staged, notificationLocalizationFile), 'utf8'),
      '<root>generated client localization</root>',
    )
    rmSync(resource)
    const producerResource = join(root, notificationLocalizationFile)
    mkdirSync(join(producerResource, '..'), { recursive: true })
    writeFileSync(producerResource, '<root>wrong producer resource</root>')
    assert.throws(
      () =>
        execFileSync(
          process.execPath,
          [script, '--stage-fixture', join(root, 'missing-client-resource')],
          {
            env: { ...process.env, VOUCHA_CONTRACT_ROOT: root },
            stdio: 'pipe',
          },
        ),
      /ENOENT/u,
    )
    assert.equal(
      readFileSync(join(staged, 'api-fixtures', 'v1', sessionFixture.bodyFile), 'utf8'),
      sessionBody,
    )
    for (const changed of [
      null,
      { method: 'GET' },
      { path: '/api/v1/session/other' },
      { status: 201 },
    ]) {
      const fixtures = [identityFixture, emailFixture]
      if (changed) fixtures.push({ ...sessionFixture, ...changed })
      writeFileSync(join(fixtureRoot, 'manifest.json'), JSON.stringify({ fixtures }))
      assert.throws(
        () =>
          execFileSync(process.execPath, [script, '--check-fixture'], {
            env: { ...process.env, VOUCHA_CONTRACT_ROOT: root },
            stdio: 'pipe',
          }),
        changed
          ? /not the canonical PATCH/u
          : /missing shared fixture native\.auth\.session\.refresh\.rotated/u,
      )
    }
    writeFileSync(
      join(fixtureRoot, 'manifest.json'),
      JSON.stringify({ fixtures: [identityFixture] }),
    )
    assert.throws(
      () =>
        execFileSync(process.execPath, [script, '--check-fixture'], {
          env: { ...process.env, VOUCHA_CONTRACT_ROOT: root },
          stdio: 'pipe',
        }),
      /missing shared fixture native\.my\.email-preferences\.default/u,
    )
    assert.throws(() =>
      execFileSync(process.execPath, [script, '--check-fixture'], {
        env: { ...process.env, VOUCHA_CONTRACT_ROOT: '/tmp/missing-fixture' },
        stdio: 'pipe',
      }),
    )
    assert.throws(() =>
      execFileSync(process.execPath, [script, '--session'], {
        env: { ...process.env },
        stdio: 'pipe',
      }),
    )
  })
})
