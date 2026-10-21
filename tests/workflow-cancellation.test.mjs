import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { spawnSync } from 'node:child_process'
import { describe, it } from 'node:test'
import { jobBlock } from './workflow-job-block.mjs'

const native = await readFile(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)
const validate = await readFile(
  new URL('../.github/workflows/validate.yml', import.meta.url),
  'utf8',
)
const command = block => {
  const script = block.match(/        run: \|\n([\s\S]+)/u)?.[1]
  assert.ok(script, 'aggregate must retain its executable result checks')
  return script.replace(/^ {10}/gmu, '')
}
const execute = (script, env) => {
  const result = spawnSync('bash', ['-e', '-o', 'pipefail', '-c', script], {
    env: { ...process.env, ...env },
    encoding: 'utf8',
  })
  assert.ifError(result.error)
  assert.equal(result.signal, null)
  return result.status
}

// Workflow wiring controls only; GitHub owns the Actions runtime semantics.
// Read the real predicate so regressions to always()/success() change these controls.
const statusPredicate = (expression, cancelled, success) => {
  const predicate = expression
    .trim()
    .replace(/^\$\{\{\s*|\s*\}\}$/gu, '')
    .replace(/\s+/gu, '')
  switch (predicate) {
    case '!cancelled()':
      return !cancelled
    case 'always()':
      return true
    case 'success()':
      return success
    default:
      assert.fail(`unsupported workflow status predicate: ${expression}`)
  }
}
const assertCancellationGate = block => {
  const expression = block.match(/^\s+if: (.+)$/mu)?.[1]
  assert.ok(expression)
  assert.equal(statusPredicate(expression, true, false), false, 'cancelled run must stop')
  assert.equal(statusPredicate(expression, false, false), true, 'real failure must be reported')
  assert.equal(statusPredicate(expression, false, true), true, 'successful run must report')
}

describe('cancellation-aware native reporting', () => {
  for (const [name, workflow] of [
    ['test-swift-android', native],
    ['native-dto-fixture-parity', native],
    ['tests', native],
    ['validate', validate],
  ]) {
    it(`${name} respects cancellation while still reporting upstream failure`, () => {
      assertCancellationGate(jobBlock(workflow, name))
    })
  }

  for (const id of ['dotnet-dto-fixture-parity', 'swift-dto-fixture-parity']) {
    it(`${id} avoids running nonessential DTO work after cancellation`, () => {
      const block = native.slice(native.indexOf(`        id: ${id}\n`))
      assert.ok(block.length < native.length)
      assertCancellationGate(block.slice(0, block.indexOf('        run:')))
    })
  }

  it('DTO advisory reports missing outcomes and genuine fixture failures', () => {
    const script = command(jobBlock(native, 'native-dto-fixture-parity'))
    const env = { DOTNET_OUTCOME: 'success', SWIFT_OUTCOME: 'success' }
    assert.equal(execute(script, env), 0)
    for (const key of Object.keys(env)) {
      for (const result of ['failure', 'skipped', 'cancelled', '']) {
        assert.notEqual(execute(script, { ...env, [key]: result }), 0)
      }
    }
  })

  it('validate aggregate rejects failure or missing results from every declared dependency', () => {
    const block = jobBlock(validate, 'validate')
    const script = command(block)
    const declared = block
      .match(/    needs: \[(.*?)\]/u)?.[1]
      .split(',')
      .map(name => name.trim())
    assert.ok(declared?.length)
    const mappings = [
      ...block.matchAll(/^          ([A-Z_]+): \$\{\{ needs\.([\w-]+)\.result \}\}$/gmu),
    ]
    assert.deepEqual(mappings.map(match => match[2]).sort(), declared.sort())
    const keys = mappings.map(match => match[1])
    const env = Object.fromEntries(keys.map(key => [key, 'success']))
    assert.equal(execute(script, env), 0)
    for (const key of keys) {
      for (const result of ['failure', 'skipped', 'cancelled', '']) {
        assert.notEqual(execute(script, { ...env, [key]: result }), 0)
      }
    }
  })

  it('Tests preserves strict prerequisites and route-gated Windows skips', () => {
    const block = jobBlock(native, 'tests')
    const script = command(block)
    const dependencies = block.match(/    needs:\n([\s\S]*?)    runs-on:/u)?.[1]
    assert.ok(dependencies)
    const keys = [...dependencies.matchAll(/^      - ([\w-]+)$/gmu)].map(m => m[1])
    assert.ok(keys.length > 0)
    const needs = Object.fromEntries(keys.map(key => [key, { result: 'success' }]))
    const invoke = (results, preview = 'true', maui = 'true', surface = 'true') =>
      execute(script, {
        NEEDS_JSON: JSON.stringify(results),
        WINDOWS_IMAGE_PREVIEW_ROUTE: preview,
        WINDOWS_LANGUAGE_MODEL_SURFACE_ROUTE: surface,
        WINDOWS_MAUI_ROUTE: maui,
      })
    assert.equal(invoke(needs), 0)
    for (const key of keys) {
      for (const result of ['failure', 'skipped', 'cancelled', '']) {
        assert.notEqual(invoke({ ...needs, [key]: { result } }), 0)
      }
    }
    const optional = {
      ...needs,
      'dotnet-windows-image-preview': { result: 'skipped' },
      'dotnet-windows-language-model-surface': { result: 'skipped' },
      'dotnet-windows-maui': { result: 'skipped' },
    }
    assert.equal(invoke(optional, 'false', 'false', 'false'), 0)
    assert.notEqual(invoke(optional, 'true', 'false', 'false'), 0)
    assert.notEqual(invoke(optional, 'false', 'true', 'false'), 0)
    assert.notEqual(invoke(optional, 'false', 'false', 'true'), 0)
  })
})
