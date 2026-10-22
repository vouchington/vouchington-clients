import { spawn } from 'node:child_process'
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'

const sharedIdentityFixtureId = 'swift.my.identity.default'
const launchArgument = '--ui-testing-launch-smoke'
const rootShellId = 'root-shell'
const bundleId = 'ai.voucha.dotnet'
const appiumPort = 4723

export function sharedIdentityFixture(root) {
  if (!root || !root.startsWith('/') || root.includes('$(') || root.includes('..'))
    throw new Error('VOUCHA_FILAMENTS_CONTRACT_ROOT must be an absolute fixture checkout')
  const fixtureRoot = join(root, 'api-fixtures', 'v1')
  const manifest = JSON.parse(readFileSync(join(fixtureRoot, 'manifest.json'), 'utf8'))
  const fixture = manifest.fixtures?.find(item => item.id === sharedIdentityFixtureId)
  if (!fixture) throw new Error(`missing shared identity fixture ${sharedIdentityFixtureId}`)
  if (fixture.method !== 'GET' || fixture.path !== '/api/v1/my/identity' || fixture.status !== 200)
    throw new Error(`shared identity fixture ${sharedIdentityFixtureId} is not the canonical GET`)
  const bodyFile = fixture.bodyFile
  if (typeof bodyFile !== 'string' || !bodyFile.startsWith('responses/') || bodyFile.includes('..'))
    throw new Error('shared identity fixture body is not a responses file')
  const bodyPath = join(fixtureRoot, bodyFile)
  if (!existsSync(bodyPath)) throw new Error(`missing fixture body ${bodyFile}`)
  return bodyPath
}

function appiumPrefix() {
  const prefix = process.env.CATALYST_APPIUM_PREFIX
  if (!prefix) throw new Error('CATALYST_APPIUM_PREFIX is required')
  return prefix
}

function wdaProject() {
  const prefix = appiumPrefix()
  const candidates = [
    join(
      prefix,
      'home',
      'node_modules',
      'appium-mac2-driver',
      'WebDriverAgentMac',
      'WebDriverAgentMac.xcodeproj',
    ),
    join(
      prefix,
      'node_modules',
      'appium-mac2-driver',
      'WebDriverAgentMac',
      'WebDriverAgentMac.xcodeproj',
    ),
  ]
  const found = candidates.find(candidate => existsSync(candidate))
  if (!found) throw new Error(`Mac2 host project is missing under ${prefix}`)
  return found
}

function run(command, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { stdio: 'inherit' })
    child.on('error', reject)
    child.on('exit', code => {
      if (code === 0) resolve()
      else reject(new Error(`${command} exited ${code}`))
    })
  })
}

async function prebuildHost() {
  await run('xcodebuild', [
    'build-for-testing',
    '-project',
    wdaProject(),
    '-scheme',
    'WebDriverAgentRunner',
    'COMPILER_INDEX_STORE_ENABLE=NO',
  ])
}

async function request(method, path, body) {
  const response = await fetch(`http://127.0.0.1:${appiumPort}${path}`, {
    method,
    ...(body
      ? { headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) }
      : {}),
  })
  const text = await response.text()
  const payload = text ? JSON.parse(text) : {}
  if (!response.ok)
    throw new Error(`${method} ${path} failed ${response.status}: ${text.slice(0, 500)}`)
  return payload
}

async function waitForReady() {
  const deadline = Date.now() + 20_000
  while (Date.now() < deadline) {
    try {
      const status = await request('GET', '/status')
      if (status.value?.ready === true || status.ready === true) return
    } catch {
      // The server is still starting.
    }
    await delay(500)
  }
  throw new Error('Appium did not become ready')
}

function elementId(payload) {
  const value = payload.value
  if (!value || typeof value !== 'object') return undefined
  return value.ELEMENT ?? value['element-6066-11e4-a52e-4f735466cecf']
}

async function findRootShell(sessionId) {
  const deadline = Date.now() + 20_000
  let lastError
  while (Date.now() < deadline) {
    try {
      const found = await request('POST', `/session/${sessionId}/element`, {
        using: 'accessibility id',
        value: rootShellId,
      })
      const id = elementId(found)
      if (id) return id
    } catch (error) {
      lastError = error
    }
    await delay(500)
  }
  throw new Error(`root-shell was not exposed to the Mac2 session: ${lastError ?? 'missing element'}`)
}

async function session() {
  const appPath = process.env.CATALYST_APP_PATH
  if (!appPath || !existsSync(appPath))
    throw new Error('CATALYST_APP_PATH must be the Debug Mac Catalyst app')
  sharedIdentityFixture(process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT)
  const prefix = appiumPrefix()
  const server = spawn(
    join(prefix, 'node_modules', '.bin', 'appium'),
    ['--address', '127.0.0.1', '--port', String(appiumPort), '--base-path', '/'],
    { env: { ...process.env, APPIUM_HOME: join(prefix, 'home') }, stdio: 'inherit' },
  )
  let sessionId
  try {
    await waitForReady()
    const created = await request('POST', '/session', {
      capabilities: {
        alwaysMatch: {
          platformName: 'mac',
          'appium:automationName': 'Mac2',
          'appium:bundleId': bundleId,
          'appium:appPath': appPath,
          'appium:arguments': [launchArgument],
          'appium:environment': {
            VOUCHA_FILAMENTS_CONTRACT_ROOT: process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT,
          },
          'appium:showServerLogs': true,
          'appium:serverStartupTimeout': 100000,
        },
      },
    })
    sessionId = created.value?.sessionId ?? created.sessionId
    if (!sessionId) throw new Error('Mac2 did not return a session id')
    await findRootShell(sessionId)
    const resultPath = process.env.CATALYST_GUI_RESULT
    if (!resultPath) throw new Error('CATALYST_GUI_RESULT is required')
    writeFileSync(resultPath, JSON.stringify({ total: 1, passed: 1, failed: 0, skipped: 0 }))
  } finally {
    if (sessionId) {
      await request('DELETE', `/session/${sessionId}`).catch(() => undefined)
    }
    server.kill('SIGTERM')
  }
}

const mode = process.argv[2]
try {
  if (mode === '--check-fixture') sharedIdentityFixture(process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT)
  else if (mode === '--prebuild-host') await prebuildHost()
  else if (mode === '--session') await session()
  else throw new Error('pass --check-fixture, --prebuild-host, or --session')
} catch (error) {
  console.error(error instanceof Error ? error.message : error)
  process.exitCode = 1
}
