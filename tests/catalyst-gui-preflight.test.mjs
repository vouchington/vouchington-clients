import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdtempSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'

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

function jobBlock(source, job) {
  const start = source.indexOf(`  ${job}:`)
  assert.notEqual(start, -1, `missing workflow job ${job}`)
  const remainder = source.slice(start)
  const nextJob = remainder.search(/\n {2}[A-Za-z0-9_-]+:/u)
  return remainder.slice(0, nextJob === -1 ? remainder.length : nextJob)
}

describe('Mac Catalyst GUI preflight', () => {
  it('keeps one non-skipped Debug Mac2 session inside a 10-minute job', () => {
    const job = jobBlock(workflow, 'dotnet-catalyst-gui')
    const maui = jobBlock(workflow, 'dotnet-maui')
    const needs = jobBlock(workflow, 'tests')

    assert.match(job, /name: Mac Catalyst GUI preflight/u)
    assert.match(job, /runs-on: macos-latest/u)
    assert.match(job, /timeout-minutes: 10/u)
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
    assert.match(job, /catalyst-gui-preflight\.mjs --session/u)
    assert.match(sessionScript, /--ui-testing-launch-smoke/u)
    assert.match(job, /pgrep -qx WindowServer/u)
    assert.match(job, /refusing to skip the Mac Catalyst GUI preflight/u)
    assert.match(
      job,
      /jq -e '\.total == 2 and \.passed == 2 and \.failed == 0 and \.skipped == 0'/u,
    )
    assert.match(needs, /^      - dotnet-catalyst-gui$/mu)
    assert.match(maui, /timeout-minutes: 30/u)
    assert.match(maui, /--configuration Release --framework net10\.0-maccatalyst/u)
    assert.doesNotMatch(maui, /appium|catalyst-gui-preflight|--configuration Debug/u)
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
    assert.match(sessionScript, /native\.my\.email-preferences\.default/u)
    assert.match(sessionScript, /total: 2, passed: 2, failed: 0, skipped: 0/u)
    assert.doesNotMatch(sessionScript, /root-shell/u)
    assert.match(notificationScript, /notification-engagement/u)
    assert.match(notificationScript, /notification-news-digest/u)
    assert.equal(job.match(/timeout-minutes:/gu)?.length, 7)
    assert.match(job, /timeout-minutes: 6\n/u)
    assert.match(job, /timeout-minutes: 3\n/u)
    assert.match(job, /timeout-minutes: 2\n/u)
    assert.match(job, /timeout-minutes: 1\n/u)
  })

  it('stages the shared identity and email-preferences fixtures', () => {
    const root = mkdtempSync(join(tmpdir(), 'catalyst-fixture-'))
    const fixtureRoot = join(root, 'api-fixtures', 'v1')
    mkdirSync(join(fixtureRoot, 'responses'), { recursive: true })
    writeFileSync(join(fixtureRoot, 'responses', 'swift.my.identity.default.json'), '{}\n')
    writeFileSync(
      join(fixtureRoot, 'responses', 'native.my.email-preferences.default.json'),
      '{"email_preferences":{}}\n',
    )
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
      JSON.stringify({ fixtures: [identityFixture, emailFixture] }),
    )
    const script = fileURLToPath(
      new URL('../dotnet-clients/tooling/catalyst-gui-preflight.mjs', import.meta.url),
    )
    execFileSync(process.execPath, [script, '--check-fixture'], {
      env: { ...process.env, VOUCHA_FILAMENTS_CONTRACT_ROOT: root },
    })
    const staged = join(root, 'staged')
    execFileSync(process.execPath, [script, '--stage-fixture', staged], {
      env: { ...process.env, VOUCHA_FILAMENTS_CONTRACT_ROOT: root },
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
    writeFileSync(
      join(fixtureRoot, 'manifest.json'),
      JSON.stringify({ fixtures: [identityFixture] }),
    )
    assert.throws(
      () =>
        execFileSync(process.execPath, [script, '--check-fixture'], {
          env: { ...process.env, VOUCHA_FILAMENTS_CONTRACT_ROOT: root },
          stdio: 'pipe',
        }),
      /missing shared fixture native\.my\.email-preferences\.default/u,
    )
    assert.throws(() =>
      execFileSync(process.execPath, [script, '--check-fixture'], {
        env: { ...process.env, VOUCHA_FILAMENTS_CONTRACT_ROOT: '/tmp/missing-fixture' },
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
