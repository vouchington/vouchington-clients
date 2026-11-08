import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const scripts = new URL('../scripts/', import.meta.url)

test('readonly trusted mounts contain the complete bootstrap module dependency closure', async () => {
  const runner = await readFile(new URL('paired-exporter-runner.mjs', scripts), 'utf8')
  const mounts = new Set(
    [...runner.matchAll(/dst=\/trusted\/([^,`]+),readonly`/gu)].map(match => match[1]),
  )
  assert.ok(mounts.has('bootstrap.mjs'))
  const pending = ['bootstrap.mjs']
  const visited = new Set()
  while (pending.length > 0) {
    const mountedName = pending.pop()
    if (visited.has(mountedName)) continue
    visited.add(mountedName)
    assert.ok(mounts.has(mountedName), `missing readonly trusted mount: ${mountedName}`)
    const sourceName =
      mountedName === 'bootstrap.mjs' ? 'paired-exporter-bootstrap.mjs' : mountedName
    const source = await readFile(new URL(sourceName, scripts), 'utf8')
    for (const match of source.matchAll(/from ['"]\.\/([^'"]+)['"]/gu)) {
      pending.push(match[1])
    }
  }
  assert.ok(visited.has('prefixed-failure.mjs'))
  assert.ok(visited.has('path-within.mjs'))
})
