import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdir, readFile, readdir, realpath, rename, rm, writeFile } from 'node:fs/promises'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { androidStateFixture } from './android-state-handoff-fixture.mjs'

const cli = fileURLToPath(
  new URL('../swift-clients/apps/android/tooling/android-kotlin-cache-cli.mjs', import.meta.url),
)
const key = 'f'.repeat(32)

test('CLI binds the selected real cache to the checked-out candidate, toolchain config and run', async t => {
  const f = await androidStateFixture(t)
  const root = await realpath(f.root)
  f.artifactRoot = join(root, 'artifact')
  const checkoutRoot = join(root, 'candidate-clients')
  await rename(f.checkoutRoot, checkoutRoot)
  const packageRoot = join(checkoutRoot, 'swift-clients/apps/android')
  const home = join(root, 'home')
  const cacheRoot = join(home, '.cache/voucha/gradle-build-cache')
  await mkdir(cacheRoot, { recursive: true })
  await writeFile(join(cacheRoot, key), 'compiled Kotlin output')
  const sources = [
    ['.mise.toml', '[tools]\nswift="6.4.0"\n'],
    ['swift-clients/apps/android/Package.resolved', '{"pins":[]}'],
    ['swift-clients/apps/android/Android/settings.gradle.kts', '// settings'],
    ['swift-clients/apps/android/Android/app/build.gradle.kts', '// app'],
    ['swift-clients/apps/android/Android/gradle.properties', 'org.gradle.jvmargs=-Xmx4g'],
    [
      'swift-clients/apps/android/Android/gradle/wrapper/gradle-wrapper.properties',
      'distributionUrl=https\\://services.gradle.org/distributions/gradle-9.4.1-bin.zip',
    ],
    [
      'swift-clients/apps/android/tooling/materialize-skip-sdk.sh',
      '# pinned toolchain materializer',
    ],
  ]
  for (const [path, text] of sources) {
    await mkdir(join(checkoutRoot, path, '..'), { recursive: true })
    await writeFile(join(checkoutRoot, path), text)
  }
  const run = (command, overrides = {}) =>
    spawnSync(process.execPath, [cli, command, packageRoot, f.artifactRoot], {
      encoding: 'utf8',
      env: {
        ...process.env,
        HOME: home,
        GITHUB_ACTIONS: 'true',
        GITHUB_WORKSPACE: root,
        CANDIDATE_REVISION: f.identity.revision,
        GITHUB_RUN_ID: f.identity.run,
        GITHUB_RUN_ATTEMPT: f.identity.attempt,
        ...overrides,
      },
    })
  const packed = run('pack')
  assert.equal(packed.status, 0, packed.stderr)
  const manifest = JSON.parse(await readFile(join(f.artifactRoot, 'manifest.json')))
  assert.equal(manifest.identity.gradle, '9.4.1')
  assert.equal(manifest.role, 'kotlin-libraries')
  assert.match(manifest.configurationSha256, /^[a-f0-9]{64}$/u)
  await rm(join(cacheRoot, key))
  const merged = run('merge')
  assert.equal(merged.status, 0, merged.stderr)
  assert.equal(await readFile(join(cacheRoot, key), 'utf8'), 'compiled Kotlin output')
  await rm(join(cacheRoot, key))
  assert.notEqual(run('merge', { CANDIDATE_REVISION: '0'.repeat(40) }).status, 0)
  assert.notEqual(run('merge', { GITHUB_RUN_ATTEMPT: '3' }).status, 0)
  await writeFile(join(checkoutRoot, '.mise.toml'), '[tools]\nswift="different"\n')
  assert.notEqual(run('merge').status, 0, 'different compiler configuration must fail')
  assert.deepEqual(await readdir(cacheRoot), [])
})
