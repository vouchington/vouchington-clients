import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
import test from 'node:test'

const read = relative => readFileSync(new URL(relative, import.meta.url), 'utf8')

const testKeys = read('../swift-clients/tooling/turnstile-test-site-keys.txt')
  .split('\n')
  .map(line => line.trim())
  .filter(line => line.length > 0)

const alwaysPass = '1x00000000000000000000AA'
const releaseExample = '0x4AAAAAAAAexample'

test('Cloudflare test keys stay aligned across the build and runtime checks', () => {
  assert.deepEqual(testKeys, [
    '1x00000000000000000000AA',
    '2x00000000000000000000AB',
    '1x00000000000000000000BB',
    '2x00000000000000000000BB',
    '3x00000000000000000000FF',
    '1x0000000000000000000000000000000AA',
    '2x0000000000000000000000000000000AA',
    '3x0000000000000000000000000000000AA',
  ])
  assert.ok(testKeys.includes(alwaysPass))
  assert.equal(testKeys.includes(releaseExample), false)
  const sources = [
    read('../swift-clients/core/Sources/VouchaCore/TurnstileSiteKeyPolicy.swift'),
    read('../dotnet-clients/src/Voucha.Client.Core/TurnstileSiteKeys.cs'),
    read('../dotnet-clients/src/Voucha.Client.App/TurnstileSiteKey.targets'),
    read('../swift-clients/apps/android/Android/app/build.gradle.kts'),
  ]
  for (const key of testKeys) {
    assert.equal(sources[0].includes(key), true, key)
    assert.equal(sources[1].includes(key), true, key)
  }
  assert.match(sources[2], /turnstile-test-site-keys\.txt/u)
  assert.match(sources[2], /1x00000000000000000000AA/u)
  assert.match(sources[3], /turnstile-test-site-keys\.txt/u)
  assert.match(sources[3], /explicit non-test VOUCHA_TURNSTILE_SITE_KEY/u)
  for (const project of [
    '../swift-clients/apps/iOS/project.yml',
    '../swift-clients/apps/macOS/project.yml',
  ]) {
    const source = read(project)
    assert.equal(source.split(alwaysPass).length - 1, 1, project)
    assert.match(source, /turnstile-release-site-key\.sh/u)
    assert.match(source, /VOUCHA_TURNSTILE_SITE_KEY: ''/u)
  }
})

test('the Release site-key script rejects missing and test keys', () => {
  const script = new URL('../swift-clients/tooling/turnstile-release-site-key.sh', import.meta.url)
  const run = key => spawnSync('sh', [script.pathname, key], { encoding: 'utf8' })
  const missing = run('  ')
  assert.notEqual(missing.status, 0)
  assert.match(missing.stderr, /explicit non-test VOUCHA_TURNSTILE_SITE_KEY/u)
  for (const key of testKeys) {
    const rejected = run(` ${key} `)
    assert.notEqual(rejected.status, 0, key)
    assert.match(rejected.stderr, /reject Cloudflare Turnstile test site keys/u)
  }
  const accepted = run(releaseExample)
  assert.equal(accepted.status, 0, accepted.stderr)
})

test('Release archives and MAUI builds pass an explicit non-test site key', () => {
  const ios = read('../.github/workflows/release-ios-testflight.yml')
  const macos = read('../.github/workflows/release-macos-unsigned.yml')
  const contracts = read('../.github/workflows/native-contract-tests.yml')
  for (const workflow of [ios, macos]) {
    assert.match(workflow, /release_site_key=0x4AAAAAAAAexample/u)
    assert.match(workflow, /VOUCHA_TURNSTILE_SITE_KEY="\$release_site_key"/u)
    assert.match(workflow, /Print :VOUCHA_TURNSTILE_SITE_KEY/u)
  }
  assert.equal(contracts.split('-p:VouchaTurnstileSiteKey=0x4AAAAAAAAexample').length - 1, 2)
  const debugStart = contracts.indexOf('name: Build the Debug app and the Mac2 host')
  const debugEnd = contracts.indexOf('name: Open the Mac2 notification settings session')
  const debugBuild = contracts.slice(debugStart, debugEnd)
  assert.equal(debugBuild.includes('-p:VouchaTurnstileSiteKey='), false)
})
