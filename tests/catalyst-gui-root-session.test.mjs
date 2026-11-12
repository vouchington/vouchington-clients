import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { assertRootShellControls } from '../dotnet-clients/tooling/catalyst-gui-root-session.mjs'
import {
  notificationChecks,
  validateNotificationEvidence,
} from '../scripts/catalyst-notification-evidence.mjs'

import { inputs, writeInputs } from './catalyst-notification-app-fixture.mjs'

const controls = ['auth-session-heading', 'auth-email-address', 'auth-send-email-code']
const nativeRevision = 'a'.repeat(40)
const { commit: producerCommit } = JSON.parse(
  readFileSync(new URL('../contracts/vouchington.json', import.meta.url), 'utf8'),
)

function webdriver({
  legacy = false,
  missing,
  hidden,
  disabled = false,
  failTimeouts = false,
} = {}) {
  const calls = []
  const request = async (method, path, payload) => {
    calls.push({ method, path, payload })
    if (path.endsWith('/timeouts')) {
      if (failTimeouts) throw new Error('Mac2 session unavailable')
      return { value: null }
    }
    if (path.endsWith('/element')) {
      assert.equal(payload.using, 'accessibility id')
      assert.ok(controls.includes(payload.value))
      if (payload.value === missing) return {}
      return {
        value: {
          [legacy ? 'ELEMENT' : 'element-6066-11e4-a52e-4f735466cecf']: `${payload.value}/native`,
        },
      }
    }
    assert.equal(method, 'GET')
    assert.ok(path.includes('%2Fnative/'))
    if (path.endsWith('/displayed')) return { value: !path.includes(hidden ?? 'never-hidden') }
    assert.ok(path.endsWith('/enabled'))
    return { value: !disabled }
  }
  return { request, calls }
}

describe('Mac Catalyst root shell controls', () => {
  for (const legacy of [false, true]) {
    it(`requires all root controls visible and the sign-in action enabled (${legacy ? 'legacy' : 'W3C'})`, async () => {
      const driver = webdriver({ legacy })
      await assertRootShellControls(driver.request, 'root-session')
      assert.deepEqual(driver.calls[0], {
        method: 'POST',
        path: '/session/root-session/timeouts',
        payload: { implicit: 75_000 },
      })
      assert.deepEqual(
        driver.calls.filter(call => call.path.endsWith('/element')).map(call => call.payload.value),
        controls,
      )
      assert.equal(driver.calls.filter(call => call.path.endsWith('/displayed')).length, 3)
      assert.equal(driver.calls.filter(call => call.path.endsWith('/enabled')).length, 1)
      assert.equal(
        driver.calls.some(call => call.path.endsWith('/click')),
        false,
      )
    })
  }

  for (const control of controls) {
    it(`fails when ${control} is absent`, async () => {
      await assert.rejects(
        assertRootShellControls(webdriver({ missing: control }).request, 'root-session'),
        new RegExp(`root control ${control} was not exposed`),
      )
    })
    it(`fails when ${control} exists but is hidden`, async () => {
      await assert.rejects(
        assertRootShellControls(webdriver({ hidden: control }).request, 'root-session'),
        new RegExp(`root control ${control} was not visible`),
      )
    })
  }

  it('fails when the visible sign-in action is disabled', async () => {
    await assert.rejects(
      assertRootShellControls(webdriver({ disabled: true }).request, 'root-session'),
      /root email sign-in action was not enabled/u,
    )
  })

  it('propagates unavailable Mac2 sessions', async () => {
    await assert.rejects(
      assertRootShellControls(webdriver({ failTimeouts: true }).request, 'root-session'),
      /Mac2 session unavailable/u,
    )
  })
})

function stagedSession() {
  const root = mkdtempSync(join(tmpdir(), 'catalyst-root-session-'))
  writeInputs(root)
  const fixtureRoot = join(root, 'api-fixtures', 'v1')
  mkdirSync(join(fixtureRoot, 'responses'), { recursive: true })
  const fixtures = [
    ['swift.my.identity.default', '/api/v1/my/identity', '{}'],
    [
      'native.my.email-preferences.default',
      '/api/v1/my/email-preferences',
      JSON.stringify({ email_preferences: inputs.preferences }),
    ],
  ].map(([id, path, body]) => {
    const bodyFile = `responses/${id}.json`
    writeFileSync(join(fixtureRoot, bodyFile), body)
    return { id, path, method: 'GET', status: 200, bodyFile }
  })
  writeFileSync(join(fixtureRoot, 'manifest.json'), JSON.stringify({ fixtures }))
  const driverUrl = new URL('./catalyst-gui-notification-driver.mjs', import.meta.url).href
  const fixtureUrl = new URL('./catalyst-notification-app-fixture.mjs', import.meta.url).href
  const harness = join(root, 'webdriver.mjs')
  writeFileSync(
    harness,
    `
import { syncBuiltinESMExports } from 'node:module'
import childProcess from 'node:child_process'
import { appendFileSync, writeFileSync } from 'node:fs'
import { nativeSession } from ${JSON.stringify(driverUrl)}
import { inputs } from ${JSON.stringify(fixtureUrl)}
const record = value => appendFileSync(process.env.CATALYST_TEST_LOG, JSON.stringify(value) + '\\n')
childProcess.spawn = (command, args) => {
  record({ command, args })
  return { kill: signal => record({ signal }) }
}
syncBuiltinESMExports()
const notification = nativeSession({ wrongField: process.env.CATALYST_TEST_FAIL_NOTIFICATION === 'true' }, inputs)
let rootShell = false
globalThis.fetch = async (url, options) => {
  const path = new URL(url).pathname
  const payload = options.body ? JSON.parse(options.body) : null
  record({ method: options.method, path, payload })
  let value = null
  if (path === '/status') value = { ready: true }
  else if (path === '/session') {
    rootShell = payload.capabilities.alwaysMatch['appium:arguments'].includes('--ui-testing-root-shell-smoke')
    value = { sessionId: 'root-session' }
    if (!rootShell) writeFileSync(process.env.CATALYST_GUI_RESULT + '.app.json', JSON.stringify(notification.evidence))
  }
  else if (path.endsWith('/source')) value = '<root-session />'
  else if (!rootShell && options.method !== 'DELETE') {
    const response = await notification.request(options.method, path.replace('/session/root-session/', '/session/native/'), payload)
    value = response.value
    writeFileSync(process.env.CATALYST_GUI_RESULT + '.app.json', JSON.stringify(notification.evidence))
  }
  else if (path.endsWith('/element')) value = { ELEMENT: payload.value }
  else if (path.endsWith('/displayed')) value = !path.includes(process.env.CATALYST_TEST_HIDDEN ?? 'never-hidden')
  else if (path.endsWith('/enabled') || path.endsWith('/attribute/enabled')) value = true
  return { ok: true, text: async () => JSON.stringify({ value }) }
}
`,
  )
  return {
    root,
    harness,
    result: join(root, 'result.json'),
    log: join(root, 'requests.jsonl'),
  }
}

function runSession(fixture, mode, hidden, revision = nativeRevision, failNotification = false) {
  const script = fileURLToPath(
    new URL('../dotnet-clients/tooling/catalyst-gui-preflight.mjs', import.meta.url),
  )
  return execFileSync(process.execPath, ['--import', fixture.harness, script, mode], {
    env: {
      ...process.env,
      VOUCHA_CONTRACT_ROOT: fixture.root,
      CATALYST_APP_PATH: fixture.root,
      CATALYST_APPIUM_PREFIX: join(fixture.root, 'appium'),
      CATALYST_GUI_RESULT: fixture.result,
      CATALYST_TEST_LOG: fixture.log,
      CATALYST_TEST_FAIL_NOTIFICATION: String(failNotification),
      CATALYST_NATIVE_REVISION: mode === '--root-session' ? '' : revision,
      ...(hidden ? { CATALYST_TEST_HIDDEN: hidden } : {}),
    },
    stdio: 'pipe',
  })
}

describe('Mac Catalyst session orchestration with external WebDriver protocol doubles', () => {
  for (const mode of ['--root-session', '--session']) {
    it(`launches the correct argument and requires its exact assertion set for ${mode}`, () => {
      const fixture = stagedSession()
      try {
        runSession(fixture, mode)
        const result = JSON.parse(readFileSync(fixture.result, 'utf8'))
        if (mode === '--root-session') {
          assert.deepEqual(result, { total: 3, passed: 3, failed: 0, skipped: 0 })
          assert.equal(existsSync(`${fixture.result}.app.json`), false)
        } else {
          validateNotificationEvidence(result, nativeRevision, producerCommit)
          assert.equal(result.total, 4)
          assert.equal(result.schemaVersion, 3)
          assert.deepEqual(
            result.checks,
            notificationChecks.map(name => ({ name, status: 'passed' })),
          )
        }
        const requests = readFileSync(fixture.log, 'utf8')
          .trim()
          .split('\n')
          .map(line => JSON.parse(line))
        const created = requests.find(request => request.path === '/session')
        assert.equal(created.payload.capabilities.alwaysMatch['appium:noReset'], false)
        assert.equal(created.payload.capabilities.alwaysMatch['appium:skipAppKill'], false)
        assert.deepEqual(created.payload.capabilities.alwaysMatch['appium:arguments'], [
          mode === '--root-session' ? '--ui-testing-root-shell-smoke' : '--ui-testing-launch-smoke',
        ])
        assert.ok(
          requests.some(
            request => request.method === 'DELETE' && request.path === '/session/root-session',
          ),
        )
        if (mode === '--session') {
          assert.deepEqual(
            requests
              .filter(request => request.payload?.script === 'macos: deepLink')
              .map(request => request.payload.args[0].url),
            ['voucha://my/preferences', 'voucha://my/notification-settings'],
          )
          assert.equal(requests.filter(request => request.path === '/session').length, 1)
        }
        assert.equal(requests.at(-1).signal, 'SIGTERM')
      } finally {
        rmSync(fixture.root, { recursive: true, force: true })
      }
    })
  }

  it('refuses stale notification evidence when the native revision is not a full SHA', () => {
    const fixture = stagedSession()
    try {
      writeFileSync(fixture.result, '{"total":4,"passed":4,"failed":0,"skipped":0}')
      assert.throws(
        () => runSession(fixture, '--session', undefined, 'short-revision'),
        /must be full SHAs/u,
      )
      assert.equal(existsSync(fixture.result), false)
      const appReport = JSON.parse(readFileSync(`${fixture.result}.app.json`, 'utf8'))
      assert.equal(new Set(appReport.patches.flatMap(patch => patch.fields)).size, 2)
      assert.equal(appReport.activations.length, 2)
      const requests = readFileSync(fixture.log, 'utf8')
        .trim()
        .split('\n')
        .map(line => JSON.parse(line))
      assert.ok(
        requests.some(
          request => request.method === 'DELETE' && request.path === '/session/root-session',
        ),
      )
      assert.equal(requests.at(-1).signal, 'SIGTERM')
    } finally {
      rmSync(fixture.root, { recursive: true, force: true })
    }
  })

  it('lists the root mode when rejecting unsupported CLI arguments', () => {
    const fixture = stagedSession()
    try {
      assert.throws(() => runSession(fixture, '--unsupported-mode'), /or --root-session/u)
      assert.equal(existsSync(fixture.result), false)
    } finally {
      rmSync(fixture.root, { recursive: true, force: true })
    }
  })

  it('keeps notification audit diagnostics without taking a failure page-source snapshot', () => {
    const fixture = stagedSession()
    try {
      assert.throws(
        () => runSession(fixture, '--session', undefined, nativeRevision, true),
        /exactly its API field/u,
      )
      assert.equal(existsSync(fixture.result), false)
      assert.equal(existsSync(`${fixture.result}.app.json`), true)
      assert.equal(existsSync(join(fixture.root, 'source.xml')), false)
      const requests = readFileSync(fixture.log, 'utf8')
        .trim()
        .split('\n')
        .map(line => JSON.parse(line))
      assert.equal(
        requests.some(request => request.path?.endsWith('/source')),
        false,
      )
      assert.ok(requests.some(request => request.method === 'DELETE'))
    } finally {
      rmSync(fixture.root, { recursive: true, force: true })
    }
  })

  it('fails the root suite, captures the native tree, and closes the session for a hidden root', () => {
    const fixture = stagedSession()
    try {
      writeFileSync(fixture.result, '{"total":3,"passed":3,"failed":0,"skipped":0}')
      assert.throws(
        () => runSession(fixture, '--root-session', 'auth-email-address'),
        /was not visible/u,
      )
      assert.equal(existsSync(fixture.result), false)
      assert.equal(readFileSync(join(fixture.root, 'source.xml'), 'utf8'), '<root-session />')
      const requests = readFileSync(fixture.log, 'utf8')
        .trim()
        .split('\n')
        .map(line => JSON.parse(line))
      assert.ok(
        requests.some(
          request => request.method === 'DELETE' && request.path === '/session/root-session',
        ),
      )
      assert.equal(requests.at(-1).signal, 'SIGTERM')
    } finally {
      rmSync(fixture.root, { recursive: true, force: true })
    }
  })
})
