import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const action = readFileSync(
  new URL('../.github/actions/setup-mise-toolchain/action.yml', import.meta.url),
  'utf8',
)
const stepIndex = name => action.indexOf(`- name: ${name}`)

test('mise .NET SDK is restored from cache before any dot.net download', () => {
  const restore = stepIndex('Restore mise-managed .NET SDK')
  assert.ok(restore > 0)
  assert.ok(restore < stepIndex('Install candidate-pinned toolchain'))
  assert.ok(restore < stepIndex('Install candidate-pinned .NET SDK on Windows'))
  assert.match(
    action,
    /id: dotnet-sdk\s+if: inputs\.tool == 'dotnet'\s+uses: actions\/cache\/restore@[0-9a-f]{40} # v/u,
  )
  assert.match(
    action,
    /path: \$\{\{ runner\.temp \}\}\/mise-data\/installs\/dotnet\/\$\{\{ steps\.dotnet-pin\.outputs\.version \}\}/u,
  )
  assert.match(
    action,
    /key: mise-dotnet-sdk-\$\{\{ runner\.os \}\}-\$\{\{ runner\.arch \}\}-\$\{\{ steps\.dotnet-pin\.outputs\.version \}\}-\$\{\{ hashFiles\(/u,
  )
  assert.match(action, /jq -er '\.sdk\.version'[^\n]*global\.json/u)
})

test('mise .NET SDK cache is saved only from main pushes after verification', () => {
  const saveName = 'Save mise-managed .NET SDK from main'
  assert.ok(stepIndex(saveName) > stepIndex('Install candidate-pinned .NET SDK on Windows'))
  const save = action.slice(stepIndex(saveName))
  assert.match(save, /github\.event_name == 'push' && github\.ref == 'refs\/heads\/main'/u)
  assert.match(save, /steps\.dotnet-sdk\.outputs\.cache-hit != 'true'/u)
  assert.match(save, /continue-on-error: true/u)
  assert.match(save, /actions\/cache\/save@[0-9a-f]{40}/u)
})

test('mise installs tolerate a slow dot.net on a cold cache', () => {
  assert.equal(action.match(/MISE_HTTP_TIMEOUT: 120s/gu)?.length, 2)
})
