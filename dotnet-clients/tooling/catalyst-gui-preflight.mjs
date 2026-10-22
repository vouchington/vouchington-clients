import { spawn } from 'node:child_process'
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
import { assertNotificationControls } from './catalyst-gui-notification-session.mjs'

const launchArgument = '--ui-testing-launch-smoke'
const bundleId = 'ai.voucha.dotnet'
const appiumPort = 4723
const requiredFixtures = [
  {
    id: 'swift.my.identity.default',
    method: 'GET',
    path: '/api/v1/my/identity',
  },
  {
    id: 'native.my.email-preferences.default',
    method: 'GET',
    path: '/api/v1/my/email-preferences',
  },
]

export function sharedIdentityFixture(root) {
  return requiredFixture(root, requiredFixtures[0])
}

function requiredFixture(root, expected) {
  if (!root || !root.startsWith('/') || root.includes('$(') || root.includes('..'))
    throw new Error('VOUCHA_FILAMENTS_CONTRACT_ROOT must be an absolute fixture checkout')
  const fixtureRoot = join(root, 'api-fixtures', 'v1')
  const manifest = JSON.parse(readFileSync(join(fixtureRoot, 'manifest.json'), 'utf8'))
  const fixture = manifest.fixtures?.find(item => item.id === expected.id)
  if (!fixture) throw new Error(`missing shared fixture ${expected.id}`)
  if (fixture.method !== expected.method || fixture.path !== expected.path || fixture.status !== 200)
    throw new Error(`shared fixture ${expected.id} is not the canonical GET`)
  const bodyFile = fixture.bodyFile
  if (typeof bodyFile !== 'string' || !bodyFile.startsWith('responses/') || bodyFile.includes('..'))
    throw new Error(`shared fixture ${expected.id} body is not a responses file`)
  const bodyPath = join(fixtureRoot, bodyFile)
  if (!existsSync(bodyPath)) throw new Error(`missing fixture body ${bodyFile}`)
  return bodyPath
}

function requireGuiFixtures(root) {
  return requiredFixtures.map(expected => requiredFixture(root, expected))
}

function stageFixture(destination) {
  if (!destination?.startsWith('/')) throw new Error('--stage-fixture requires an absolute directory')
  const root = process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT
  const bodyPaths = requireGuiFixtures(root)
  const sourceRoot = join(root, 'api-fixtures', 'v1')
  mkdirSync(join(destination, 'api-fixtures', 'v1'), { recursive: true })
  copyFileSync(join(sourceRoot, 'manifest.json'), join(destination, 'api-fixtures', 'v1', 'manifest.json'))
  for (const bodyPath of bodyPaths) {
    const relativeBody = bodyPath.slice(sourceRoot.length + 1)
    const targetBody = join(destination, 'api-fixtures', 'v1', relativeBody)
    mkdirSync(dirname(targetBody), { recursive: true })
    copyFileSync(bodyPath, targetBody)
  }
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

async function session() {
  const appPath = process.env.CATALYST_APP_PATH
  if (!appPath || !existsSync(appPath))
    throw new Error('CATALYST_APP_PATH must be the Debug Mac Catalyst app')
  requireGuiFixtures(process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT)
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
    await assertNotificationControls(request, sessionId)
    const resultPath = process.env.CATALYST_GUI_RESULT
    if (!resultPath) throw new Error('CATALYST_GUI_RESULT is required')
    writeFileSync(resultPath, JSON.stringify({ total: 2, passed: 2, failed: 0, skipped: 0 }))
  } finally {
    if (sessionId) {
      await request('DELETE', `/session/${sessionId}`).catch(() => undefined)
    }
    server.kill('SIGTERM')
  }
}

const mode = process.argv[2]
try {
  if (mode === '--check-fixture') requireGuiFixtures(process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT)
  else if (mode === '--stage-fixture') stageFixture(process.argv[3])
  else if (mode === '--prebuild-host') await prebuildHost()
  else if (mode === '--session') await session()
  else throw new Error('pass --check-fixture, --stage-fixture, --prebuild-host, or --session')
} catch (error) {
  console.error(error instanceof Error ? error.message : error)
  process.exitCode = 1
}
