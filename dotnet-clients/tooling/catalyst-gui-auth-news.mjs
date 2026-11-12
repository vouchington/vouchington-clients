import { execFile, spawn } from 'node:child_process'
import { copyFileSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { createServer } from 'node:net'
import { dirname, join } from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
import { promisify } from 'node:util'
import { pathToFileURL } from 'node:url'
import { assertAuthNewsControls } from './catalyst-gui-auth-news-session.mjs'
import { authNewsBudgetMs, authNewsCleanupMs, createDeadline } from './catalyst-gui-deadline.mjs'

export function requireAuthNewsFixtures(root) {
  if (!root?.startsWith('/') || root.includes('..') || root.includes('$(')) throw new Error('Absolute contract root required')
  const fixtureRoot = join(root, 'api-fixtures', 'v1')
  const manifest = JSON.parse(readFileSync(join(fixtureRoot, 'manifest.json'), 'utf8'))
  for (const [id, path] of [
    ['swift.my.identity.default', '/api/v1/my/identity'],
    ['web.rss-feed-items.feed.default', '/api/v1/feeds/rss_feed_items/any'],
  ]) {
    const fixture = manifest.fixtures.find(item => item.id === id)
    if (fixture?.method !== 'GET' || fixture.path !== path || fixture.status !== 200 ||
      !fixture.bodyFile?.startsWith('responses/') || fixture.bodyFile.includes('..')) throw new Error(`Invalid fixture ${id}`)
    JSON.parse(readFileSync(join(fixtureRoot, fixture.bodyFile), 'utf8'))
  }
}

function stageFixture(destination) {
  if (!destination?.startsWith('/') || destination.includes('..')) throw new Error('Absolute staging directory required')
  const root = process.env.VOUCHA_CONTRACT_ROOT
  requireAuthNewsFixtures(root)
  const source = join(root, 'api-fixtures', 'v1')
  const manifest = JSON.parse(readFileSync(join(source, 'manifest.json'), 'utf8'))
  const fixtures = manifest.fixtures.filter(item => ['swift.my.identity.default', 'web.rss-feed-items.feed.default'].includes(item.id))
  const target = join(destination, 'api-fixtures', 'v1')
  mkdirSync(target, { recursive: true })
  writeFileSync(join(target, 'manifest.json'), JSON.stringify({ ...manifest, fixtures }))
  for (const fixture of fixtures) {
    const body = join(target, fixture.bodyFile)
    mkdirSync(dirname(body), { recursive: true })
    copyFileSync(join(source, fixture.bodyFile), body)
  }
}

async function session() {
  const budget = createDeadline(authNewsBudgetMs)
  let cleanupBudget
  const cleanup = () => cleanupBudget ??= createDeadline(authNewsCleanupMs)
  const resultPath = process.env.CATALYST_FLOWS_RESULT
  if (resultPath) rmSync(resultPath, { force: true })
  const prefix = process.env.CATALYST_APPIUM_PREFIX
  const appPath = process.env.CATALYST_APP_PATH
  if (!resultPath || !prefix || !appPath || !existsSync(appPath)) throw new Error('Flow result, Appium prefix, and Debug app path required')
  const nativeRevision = process.env.CATALYST_NATIVE_REVISION
  const { commit } = JSON.parse(readFileSync('contracts/vouchington.json', 'utf8'))
  if (![nativeRevision, commit].every(value => typeof value === 'string' && /^[a-f0-9]{40}$/u.test(value)))
    throw new Error('Expected native and producer revisions must be full SHAs')
  requireAuthNewsFixtures(process.env.VOUCHA_CONTRACT_ROOT)
  await new Promise((resolve, reject) => {
    const probe = createServer()
    probe.once('error', () => reject(new Error('Auth/news Appium port 4727 is already occupied')))
    probe.listen(4727, '127.0.0.1', () => probe.close(resolve))
  })
  const server = spawn(join(prefix, 'node_modules', '.bin', 'appium'), ['--address', '127.0.0.1', '--port', '4727', '--base-path', '/'],
    { env: { ...process.env, APPIUM_HOME: join(prefix, 'home') }, stdio: 'inherit' })
  let sessionId
  let checks
  let failure
  let startupError
  const serverExit = new Promise(resolve => {
    server.once('exit', resolve)
    server.once('error', error => { startupError = error; resolve() })
  })
  const sessionStartupTimeout = 100_000
  async function request(method, path, body, timeout = 10_000, deadline = budget) {
    const response = await fetch(`http://127.0.0.1:4727${path}`, { method, signal: AbortSignal.any([deadline.signal, AbortSignal.timeout(deadline.remaining(timeout))]),
      ...(body ? { headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) } : {}) })
    const payload = await response.json()
    if (!response.ok || payload.value?.error) throw new Error(`${method} ${path}: ${JSON.stringify(payload).slice(0, 500)}`)
    return payload
  }
  try {
    checks = await budget.run(async () => {
    const deadline = Date.now() + 20_000
    let ready = false
    while (Date.now() < deadline) {
      if (startupError) throw startupError
      if (server.exitCode !== null) throw new Error(`Appium exited before readiness: ${server.exitCode}`)
      try { const status = await request('GET', '/status'); ready = status.value?.ready === true || status.ready === true } catch { /* starting */ }
      if (ready) break
      await budget.pause(500)
    }
    if (!ready) throw new Error('Appium did not become ready')
    const created = await request('POST', '/session', { capabilities: { alwaysMatch: {
      platformName: 'mac', 'appium:automationName': 'Mac2', 'appium:bundleId': 'ai.voucha.dotnet',
      'appium:appPath': appPath, 'appium:noReset': false, 'appium:skipAppKill': false,
      'appium:arguments': ['--ui-testing-auth-news-flows'],
      'appium:environment': { VOUCHA_CONTRACT_ROOT: process.env.VOUCHA_CONTRACT_ROOT,
        VOUCHA_API_BASE_URL: 'https://ui-testing.voucha.invalid', VOUCHA_WEB_BASE_URL: 'https://ui-testing.voucha.invalid' },
      'appium:showServerLogs': true, 'appium:serverStartupTimeout': budget.remaining(sessionStartupTimeout, 10_000),
    } } }, sessionStartupTimeout + 10_000)
    sessionId = created.value?.sessionId ?? created.sessionId
    if (!sessionId) throw new Error('Mac2 returned no session id')
    return assertAuthNewsControls(request, sessionId, () =>
      promisify(execFile)('open', ['-a', appPath, 'voucha://feed/news'], { timeout: budget.remaining(10_000) }), budget)
    })
  } catch (error) {
    failure = error
    if (sessionId) {
      try { const source = await request('GET', `/session/${sessionId}/source`, undefined, 2_000, cleanup()); writeFileSync(join(dirname(resultPath), 'auth-news-source.xml'), String(source.value).slice(0, 200_000)) } catch { /* preserve original failure */ }
    }
    throw error
  } finally {
    try {
      await cleanup().run(async () => {
        let cleanupError
        if (sessionId) {
          try { await request('DELETE', `/session/${sessionId}`, undefined, 3_000, cleanup()) }
          catch (error) { cleanupError = error; console.error(`Mac2 teardown failed: ${error}`) }
        }
        server.kill('SIGTERM')
        const stopped = await Promise.race([serverExit.then(() => true), delay(cleanup().remaining(5_000), undefined, { ref: false }).then(() => false)])
        if (!stopped) { server.kill('SIGKILL'); await serverExit }
        if (cleanupError) throw cleanupError
      })
    } catch (error) {
      server.kill('SIGKILL')
      if (!failure) throw error
    }
  }
  const { authNewsEvidence } = await import('../../scripts/catalyst-auth-news-evidence.mjs')
  writeFileSync(resultPath, JSON.stringify(authNewsEvidence(checks, nativeRevision, commit)))
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    if (process.argv[2] === '--check-fixture') requireAuthNewsFixtures(process.env.VOUCHA_CONTRACT_ROOT)
    else if (process.argv[2] === '--stage-fixture') stageFixture(process.argv[3])
    else if (process.argv[2] === '--session') await session()
    else throw new Error('Pass --check-fixture, --stage-fixture, or --session')
  } catch (error) { console.error(error instanceof Error ? error.message : error); process.exitCode = 1 }
}
