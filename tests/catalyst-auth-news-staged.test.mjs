import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { execFile } from 'node:child_process'
import { promisify } from 'node:util'
import { fileURLToPath } from 'node:url'
import {
  authNewsFixtures,
  requireAuthNewsFixtures,
} from '../dotnet-clients/tooling/catalyst-gui-auth-news.mjs'

const exec = promisify(execFile)
const cli = fileURLToPath(
  new URL('../dotnet-clients/tooling/catalyst-gui-auth-news.mjs', import.meta.url),
)

async function withProducerRoot(action, omit = () => false) {
  const root = mkdtempSync(join(tmpdir(), 'catalyst-staged-'))
  const fixtures = join(root, 'api-fixtures', 'v1')
  mkdirSync(join(fixtures, 'responses'), { recursive: true })
  const entries = authNewsFixtures
    .filter(([id]) => !omit(id))
    .map(([id, method, path], index) => {
      writeFileSync(join(fixtures, 'responses', `${index}.json`), '{}')
      return { id, method, path, status: 200, bodyFile: `responses/${index}.json` }
    })
  writeFileSync(join(fixtures, 'manifest.json'), JSON.stringify({ fixtures: entries }))
  try {
    await action(root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

describe('Catalyst auth/news staged contract', () => {
  it('stages every fixture the app handler reads at construction, including native login', async () => {
    assert.ok(
      authNewsFixtures.some(([id]) => id === 'native.auth.email-address.login.authenticated'),
    )
    await withProducerRoot(async root => {
      const staged = join(root, 'staged')
      const env = { ...process.env, VOUCHA_CONTRACT_ROOT: root }
      await exec(process.execPath, [cli, '--stage-fixture', staged], { env })
      const manifest = JSON.parse(
        readFileSync(join(staged, 'api-fixtures', 'v1', 'manifest.json'), 'utf8'),
      )
      assert.deepEqual(
        manifest.fixtures.map(item => item.id).sort(),
        authNewsFixtures.map(([id]) => id).sort(),
      )
      for (const item of manifest.fixtures)
        assert.ok(existsSync(join(staged, 'api-fixtures', 'v1', item.bodyFile)), item.bodyFile)
    })
  })

  it('rejects a producer root without the native login fixture before launching Mac2', async () => {
    await withProducerRoot(
      root =>
        assert.throws(
          () => requireAuthNewsFixtures(root),
          /Invalid fixture native.auth.email-address.login.authenticated/u,
        ),
      id => id.startsWith('native.auth'),
    )
  })
})
