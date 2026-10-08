import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

test('native candidate setup provisions pnpm for the harness', async () => {
  const action = await readFile(
    new URL('../.github/actions/prepare-native-contract/action.yml', import.meta.url),
    'utf8',
  )
  const setupIndex = action.indexOf('name: Set up pnpm')
  const installIndex = action.indexOf('name: Install candidate tooling')

  assert.ok(setupIndex >= 0 && setupIndex < installIndex)
  assert.match(
    action.slice(setupIndex, installIndex),
    /uses: pnpm\/action-setup@[0-9a-f]{40}[\s\S]*?version: [^\s]+/u,
  )
  assert.match(action.slice(installIndex), /run: pnpm install --frozen-lockfile/u)
})
