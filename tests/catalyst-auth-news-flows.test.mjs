import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { createServer } from 'node:net'
import { join } from 'node:path'
import { execFile } from 'node:child_process'
import { promisify } from 'node:util'
import { fileURLToPath } from 'node:url'
import { requireAuthNewsFixtures } from '../dotnet-clients/tooling/catalyst-gui-auth-news.mjs'
import { assertAuthNewsControls } from '../dotnet-clients/tooling/catalyst-gui-auth-news-session.mjs'

function backend({
  initiallyAuthenticated = false,
  initiallyExpanded = false,
  wrongRelatedTitle = false,
  losesPrimary = false,
} = {}) {
  let authenticated = initiallyAuthenticated
  let expanded = initiallyExpanded
  let requested = false
  const values = {}
  const calls = []
  async function request(method, path, body) {
    calls.push({ method, path, body })
    if (path.endsWith('/element')) return { value: { ELEMENT: body.value } }
    const match = path.match(/\/element\/([^/]+)\/(.*)/u)
    assert.ok(match, `Unexpected request ${path}`)
    const [, id, command] = match
    if (command === 'value') {
      values[id] = body.text
      return { value: null }
    }
    if (command === 'click') {
      if (id === 'auth-send-email-code') {
        assert.equal(values['auth-email-address'], 'fixture@example.test')
        requested = true
      }
      if (id === 'auth-verify-email-code') {
        assert.ok(requested)
        assert.equal(values['auth-one-time-code'], 'ABCDEFGHJK')
        authenticated = true
      }
      if (id === 'navigation-news') assert.ok(authenticated)
      if (id === 'story-related-articles') expanded = !expanded
      return { value: null }
    }
    if (command === 'displayed')
      return {
        value:
          id === 'news-related-title-item-peer'
            ? expanded
            : id === 'auth-resend-email-code'
              ? requested
              : id === 'news-item-title-item-1'
                ? !losesPrimary
                : true,
      }
    if (command === 'attribute/value')
      return {
        value:
          id === 'auth-session-label'
            ? authenticated
              ? 'Signed in as alice'
              : 'Signed out'
            : id === 'news-item-title-item-1'
              ? 'Test Article'
              : id === 'news-related-title-item-peer'
                ? wrongRelatedTitle
                  ? 'Wrong story'
                  : 'Related cluster article'
                : '',
      }
    throw new Error(`Unexpected command ${command}`)
  }
  return { request, calls }
}

describe('Catalyst auth/news Mac2 protocol assertions (not GUI execution)', () => {
  it('drives email controls, authenticated news navigation, and visible expand/collapse', async () => {
    const fake = backend()
    const checks = await assertAuthNewsControls(fake.request, 'flow', async () => {})
    assert.deepEqual(
      checks.map(check => check.name),
      [
        'anonymous-session-visible',
        'email-otp-request-accepted',
        'email-otp-session-authenticated',
        'news-fixture-row-visible',
        'related-article-expanded',
        'related-article-collapsed',
      ],
    )
    assert.equal(
      fake.calls.filter(call => call.path.endsWith('/story-related-articles/click')).length,
      2,
    )
    assert.ok(checks.every(check => check.status === 'passed'))
  })
  it('rejects a related row already exposed before expansion', async () => {
    await assert.rejects(
      assertAuthNewsControls(backend({ initiallyExpanded: true }).request, 'flow', async () => {}),
      /before expansion/u,
    )
  })
  it('rejects a different related article despite a successful click', async () => {
    await assert.rejects(
      assertAuthNewsControls(backend({ wrongRelatedTitle: true }).request, 'flow', async () => {}),
      /Wrong related article/u,
    )
  })
})

describe('Catalyst auth/news fixture prerequisites', () => {
  it('rejects missing and incorrectly routed fixtures before launching Mac2', () => {
    const root = mkdtempSync(join(tmpdir(), 'auth-news-contract-'))
    try {
      const path = join(root, 'api-fixtures', 'v1')
      mkdirSync(path, { recursive: true })
      writeFileSync(join(path, 'manifest.json'), JSON.stringify({ fixtures: [] }))
      assert.throws(
        () => requireAuthNewsFixtures(root),
        /Invalid fixture swift.my.identity.default/u,
      )
      writeFileSync(
        join(path, 'manifest.json'),
        JSON.stringify({
          fixtures: [
            {
              id: 'swift.my.identity.default',
              method: 'POST',
              path: '/api/v1/my/identity',
              status: 200,
              bodyFile: 'responses/identity.json',
            },
          ],
        }),
      )
      assert.throws(
        () => requireAuthNewsFixtures(root),
        /Invalid fixture swift.my.identity.default/u,
      )
      assert.throws(() => requireAuthNewsFixtures('relative-root'), /Absolute contract root/u)
    } finally {
      rmSync(root, { recursive: true, force: true })
    }
  })
})

const exec = promisify(execFile)
const cli = fileURLToPath(
  new URL('../dotnet-clients/tooling/catalyst-gui-auth-news.mjs', import.meta.url),
)
const fakeAppium = fileURLToPath(new URL('./catalyst-auth-news-fake-appium.mjs', import.meta.url))

async function cliFixture(action) {
  const root = mkdtempSync(join(tmpdir(), 'catalyst-auth-news-cli-'))
  const prefix = join(root, 'appium')
  mkdirSync(join(prefix, 'node_modules', '.bin'), { recursive: true })
  const executable = join(prefix, 'node_modules', '.bin', 'appium')
  writeFileSync(executable, `#!/bin/sh\nexec '${process.execPath}' '${fakeAppium}' "$@"\n`)
  chmodSync(executable, 0o755)
  const open = join(prefix, 'node_modules', '.bin', 'open')
  writeFileSync(open, `#!/bin/sh\nexec '${process.execPath}' '${fakeAppium}' --open "$@"\n`)
  chmodSync(open, 0o755)
  const fixtures = join(root, 'api-fixtures', 'v1')
  mkdirSync(join(fixtures, 'responses'), { recursive: true })
  writeFileSync(join(fixtures, 'responses', 'identity.json'), '{}')
  writeFileSync(join(fixtures, 'responses', 'news.json'), '{}')
  writeFileSync(
    join(fixtures, 'manifest.json'),
    JSON.stringify({
      fixtures: [
        {
          id: 'swift.my.identity.default',
          method: 'GET',
          path: '/api/v1/my/identity',
          status: 200,
          bodyFile: 'responses/identity.json',
        },
        {
          id: 'web.rss-feed-items.feed.default',
          method: 'GET',
          path: '/api/v1/feeds/rss_feed_items/any',
          status: 200,
          bodyFile: 'responses/news.json',
        },
      ],
    }),
  )
  const result = join(root, 'result.json')
  const log = join(root, 'appium.jsonl')
  writeFileSync(result, '{"stale":true}')
  const env = {
    ...process.env,
    CATALYST_APPIUM_PREFIX: prefix,
    CATALYST_APP_PATH: root,
    CATALYST_FLOWS_RESULT: result,
    VOUCHA_CONTRACT_ROOT: root,
    CATALYST_NATIVE_REVISION: 'a'.repeat(40),
    CATALYST_FAKE_LOG: log,
    PATH: `${join(prefix, 'node_modules', '.bin')}:${process.env.PATH}`,
  }
  try {
    await action({ root, result, log, env })
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

describe('Catalyst auth/news CLI orchestration with a child fake (not GUI evidence)', () => {
  it('starts a fresh app on its own port and tears down the session/server before writing evidence', async () => {
    await cliFixture(async ({ result, log, env }) => {
      await exec(process.execPath, [cli, '--session'], { env, timeout: 15000 })
      const calls = readFileSync(log, 'utf8')
        .trim()
        .split('\n')
        .map(line => JSON.parse(line))
      const caps = calls.find(call => call.path === '/session').body.capabilities.alwaysMatch
      assert.equal(calls[0].port, 4727)
      assert.deepEqual(calls.find(call => call.open)?.open, [
        '-a',
        env.CATALYST_APP_PATH,
        'voucha://feed/news',
      ])
      assert.equal(caps['appium:noReset'], false)
      assert.equal(caps['appium:skipAppKill'], false)
      assert.deepEqual(caps['appium:arguments'], ['--ui-testing-auth-news-flows'])
      assert.ok(
        calls.some(call => call.method === 'DELETE' && call.path === '/session/fake-session'),
      )
      assert.equal(calls.at(-1).stopped, true)
      assert.equal(JSON.parse(readFileSync(result, 'utf8')).passed, 6)
    })
  })
  it('clears stale success before invalid revision can launch a server', async () => {
    await cliFixture(async ({ result, log, env }) => {
      await assert.rejects(
        exec(process.execPath, [cli, '--session'], {
          env: { ...env, CATALYST_NATIVE_REVISION: 'main' },
        }),
        /full SHAs/u,
      )
      assert.throws(() => readFileSync(result), /ENOENT/u)
      assert.throws(() => readFileSync(log), /ENOENT/u)
    })
  })
  it('refuses an occupied port instead of handing off to a stale Appium server', async () => {
    await cliFixture(async ({ result, log, env }) => {
      const server = createServer()
      await new Promise(resolve => server.listen(4727, '127.0.0.1', resolve))
      try {
        await assert.rejects(
          exec(process.execPath, [cli, '--session'], { env }),
          /already occupied/u,
        )
        assert.throws(() => readFileSync(result), /ENOENT/u)
        assert.throws(() => readFileSync(log), /ENOENT/u)
      } finally {
        await new Promise(resolve => server.close(resolve))
      }
    })
  })
  it('keeps the assertion failure when diagnostics fail and still deletes session and kills server', async () => {
    await cliFixture(async ({ result, log, env }) => {
      await assert.rejects(
        exec(process.execPath, [cli, '--session'], {
          env: { ...env, CATALYST_FAKE_FAIL: 'assertion' },
          timeout: 15000,
        }),
        /Wrong related article/u,
      )
      assert.throws(() => readFileSync(result), /ENOENT/u)
      const calls = readFileSync(log, 'utf8')
        .trim()
        .split('\n')
        .map(line => JSON.parse(line))
      assert.ok(calls.some(call => call.path?.endsWith('/source')))
      assert.ok(calls.some(call => call.method === 'DELETE'))
      assert.equal(calls.at(-1).stopped, true)
    })
  })
})
