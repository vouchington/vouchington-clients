import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { chmod, copyFile, mkdir, mkdtemp, readFile, readdir, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'

const repository = new URL('../', import.meta.url)

test('host-only materialization verifies and seeds both Skip URLs without native provisioning', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-host-materialize-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const tooling = join(root, 'swift-clients/apps/android/tooling')
  const helper = join(root, 'swift-clients/tooling/cached-archive.sh')
  const home = join(root, 'home')
  const downloads = join(root, 'downloads')
  const bin = join(root, 'bin')
  for (const path of [tooling, dirname(helper), home, downloads, bin])
    await mkdir(path, { recursive: true })
  await copyFile(new URL('swift-clients/tooling/cached-archive.sh', repository), helper)
  const payload = 'verified fixture Skip archive'
  const checksum = createHash('sha256').update(payload).digest('hex')
  const source = await readFile(
    new URL('swift-clients/apps/android/tooling/materialize-skip-sdk.sh', repository),
    'utf8',
  )
  const script = join(tooling, 'materialize-skip-sdk.sh')
  await writeFile(
    script,
    source.replace(/SKIP_MACOS_ZIP_SHA256="[a-f0-9]+"/u, `SKIP_MACOS_ZIP_SHA256="${checksum}"`),
  )
  for (const tool of ['mise', 'swift', 'unzip', 'curl']) {
    const path = join(bin, tool)
    await writeFile(path, '#!/bin/bash\necho "unexpected native provisioning" >&2\nexit 93\n')
    await chmod(path, 0o755)
  }
  const archive = join(downloads, 'skip-macos-1.9.13.zip')
  await writeFile(archive, payload)
  const env = {
    ...process.env,
    HOME: home,
    PATH: `${bin}:${process.env.PATH}`,
    RUNNER_TEMP: root,
    VOUCHA_SKIP_SWIFT_HOME: join(root, 'skip-home'),
    VOUCHA_SWIFT_ANDROID_DOWNLOAD_DIR: downloads,
  }
  const result = spawnSync('bash', [script, '--host-only'], { env, encoding: 'utf8' })
  assert.equal(result.status, 0, result.stderr)
  const artifacts = join(home, 'Library/Caches/org.swift.swiftpm/artifacts')
  const files = await readdir(artifacts)
  assert.equal(files.length, 2)
  assert.ok(files.some(name => name.startsWith('https___source_skip_tools_')))
  assert.ok(files.some(name => name.startsWith('https___github_com_skiptools_')))
  for (const file of files) assert.equal(await readFile(join(artifacts, file), 'utf8'), payload)
  assert.deepEqual(await readdir(downloads), ['skip-macos-1.9.13.zip'])
  assert.deepEqual(await readdir(join(root, 'skip-home')), [])

  await writeFile(archive, 'corrupted archive')
  const corrupted = spawnSync('bash', [script, '--host-only'], { env, encoding: 'utf8' })
  assert.notEqual(corrupted.status, 0)
  assert.match(corrupted.stderr, /unexpected native provisioning/u)
  const invalid = spawnSync('bash', [script, '--unknown'], { env, encoding: 'utf8' })
  assert.equal(invalid.status, 64)
})

test('host CI resolves with the verified host compiler and keeps binary cache enforcement', async () => {
  const workflow = await readFile(
    new URL('.github/workflows/native-contract-tests.yml', repository),
    'utf8',
  )
  const host = workflow
    .split('  test-swift-android-host:\n')[1]
    .split('  test-swift-android-cookie:\n')[0]
  assert.match(host, /host-only: true/u)
  assert.doesNotMatch(host, /Setup Android SDK|setup-mise-toolchain/u)
  assert.ok(
    host.indexOf('verify-android-host-swift.sh') < host.indexOf('Resolve Android Swift package'),
  )
  assert.match(
    host,
    /MISE_EXEC_AUTO_INSTALL=false mise exec swift@6\.3\.3 -- swift package.*--force-resolved-versions/u,
  )
  assert.match(host, /! grep -E 'Downloading binary artifact'/u)
  assert.match(host, /run-android-host-swift-tests\.sh[\s\S]*--filter VouchaAndroidTests/u)
})

test('native test-library CI retains cross-compilation without APK or emulator setup', async () => {
  const workflow = await readFile(
    new URL('.github/workflows/native-contract-tests.yml', repository),
    'utf8',
  )
  const libs = workflow
    .split('  test-swift-android-libs:\n')[1]
    .split('  test-swift-android-host:\n')[0]
  assert.doesNotMatch(libs, /Resolve Java|Setup Android SDK|select-xcode/u)
  assert.match(libs, /setup-mise-toolchain[\s\S]*tool: swift/u)
  assert.match(libs, /prepare-android-archives/u)
  assert.match(libs, /VOUCHA_SKIP_ANDROID_HOST_SWIFT_TEST: '1'/u)
  assert.match(libs, /ANDROID_BUILD_PHASE: test-libs/u)
})

test('host and library shards skip Node dependencies while preserving contract identity', async () => {
  const workflow = await readFile(
    new URL('.github/workflows/native-contract-tests.yml', repository),
    'utf8',
  )
  for (const [name, next] of [
    ['test-swift-android-libs', 'test-swift-android-host'],
    ['test-swift-android-host', 'test-swift-android-cookie'],
  ]) {
    const job = workflow.split(`  ${name}:\n`)[1].split(`  ${next}:\n`)[0]
    assert.match(
      job,
      /prepare-native-contract[\s\S]*candidate-revision-sha:.*needs\.verify\.outputs\.revision-sha/u,
    )
    assert.match(job, /producer-run-attempt:.*github\.run_attempt/u)
    assert.match(job, /producer-run-id:.*github\.run_id/u)
    assert.match(job, /skip-candidate-install: true/u)
    assert.match(job, /--force-resolved-versions/u)
    assert.doesNotMatch(job, /(?:pnpm|npm) (?:run|exec|install) /u)
  }
  const action = await readFile(
    new URL('.github/actions/prepare-native-contract/action.yml', repository),
    'utf8',
  )
  for (const name of [
    'Verify artifact and candidate identity',
    'Check out exact candidate revision',
  ]) {
    const step = action.split(`    - name: ${name}\n`)[1].split('    - name:')[0]
    assert.doesNotMatch(step, /skip-candidate-install/u)
  }
})
