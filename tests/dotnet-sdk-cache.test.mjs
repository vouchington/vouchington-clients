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

test('cold-cache .NET SDK is seeded from the release CDN, never dot.net, before mise install', () => {
  const seedName = 'Seed .NET SDK from builds.dotnet.microsoft.com'
  const posix = stepIndex(seedName)
  const windows = stepIndex(`${seedName} on Windows`)
  const install = stepIndex('Install candidate-pinned toolchain')
  assert.ok(stepIndex('Restore mise-managed .NET SDK') < posix)
  assert.ok(posix < windows)
  assert.ok(windows < install)
  const seeds = action.slice(posix, install)
  assert.equal(seeds.match(/steps\.dotnet-sdk\.outputs\.cache-hit != 'true'/gu)?.length, 2)
  assert.doesNotMatch(seeds, /https?:\/\/dot\.net/u)
  assert.match(seeds, /release-metadata\/\$\{channel\}\/releases\.json/u)
  assert.match(seeds, /release-metadata\/\$channel\/releases\.json/u)
  for (const rid of ['linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64', 'win-x64', 'win-arm64']) {
    assert.ok(seeds.includes(rid), rid)
  }
  assert.match(seeds, /--retry 5 --retry-all-errors/u)
  assert.match(seeds, /openssl dgst -sha512/u)
  assert.match(seeds, /Get-FileHash -Algorithm SHA512/u)
  assert.equal(seeds.match(/SHA-512 mismatch/gu)?.length, 2)
  assert.match(seeds, /mise-data\/installs\/dotnet\/\$DOTNET_VERSION/u)
  assert.match(seeds, /mise-data\/installs\/dotnet\/\$version/u)
})

test('mise installs tolerate a slow dot.net on a cold cache', () => {
  assert.equal(action.match(/MISE_HTTP_TIMEOUT: 120s/gu)?.length, 2)
})
