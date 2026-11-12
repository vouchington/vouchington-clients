import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { existsSync } from 'node:fs'
import { mkdir, mkdtemp, readFile, realpath, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { promisify } from 'node:util'
import test from 'node:test'

import { writeExecutable } from './fixture-files.mjs'

const execFileAsync = promisify(execFile)
const repositoryRoot = resolve(import.meta.dirname, '..')
const harnessPath = join(repositoryRoot, 'dotnet-clients/tooling/harness.sh')
const harnessTools = [
  'cat',
  'dirname',
  'env',
  'mktemp',
  'node',
  'od',
  'grep',
  'tail',
  'realpath',
  'rm',
  'tr',
  'whoami',
]

async function isolatedPath(t, leadingDirectories) {
  const tools = await mkdtemp(join(tmpdir(), 'voucha-dotnet-tools-'))
  t.after(() => rm(tools, { recursive: true, force: true }))
  for (const name of harnessTools) {
    const source =
      name === 'node'
        ? process.execPath
        : ['/usr/bin', '/bin'].map(directory => join(directory, name)).find(existsSync)
    if (source) await symlink(source, join(tools, name))
  }
  return [...leadingDirectories, tools].join(':')
}

function baseEnvironment() {
  const environment = { ...process.env }
  delete environment.DOTNET_INSTALL_DIR
  delete environment.DOTNET_MULTILEVEL_LOOKUP
  delete environment.DOTNET_ROOT
  delete environment.GITHUB_ACTIONS
  return environment
}

async function writeDotnetStub(directory, version = '10.0.401') {
  await mkdir(directory, { recursive: true })
  const host = join(directory, 'dotnet')
  await writeExecutable(host, `#!/bin/bash\nprintf '%s\\n' '${version}'\n`)
  await mkdir(join(directory, 'sdk'), { recursive: true })
  return realpath(host)
}

async function writeMiseStub(directory) {
  await mkdir(directory, { recursive: true })
  await writeExecutable(
    join(directory, 'mise'),
    `#!/bin/bash
set -euo pipefail
case "${'${1:-}'}" in
  where) [[ "${'${2:-}'}" == dotnet ]] && printf '%s\\n' "$MISE_TEST_DOTNET_ROOT" ;;
  which) [[ "${'${2:-}'}" == dotnet ]] && printf '%s\\n' "$MISE_TEST_DOTNET_HOST" ;;
  exec)
    shift
    [[ "${'${1:-}'}" == -- ]]
    shift
    [[ "${'${1:-}'}" == dotnet ]]
    shift
    exec "$MISE_TEST_DOTNET_HOST" "$@"
    ;;
  *) echo "unexpected mise command: $*" >&2; exit 2 ;;
esac
`,
  )
}

async function invokeHarness(
  environment,
  command = ['/bin/bash', '-c', 'printf \'%s\\n\' "$VOUCHA_DOTNET_HOST" "$DOTNET_ROOT"'],
) {
  return execFileAsync('/bin/bash', [harnessPath, '--exec', ...command], {
    cwd: repositoryRoot,
    encoding: 'utf8',
    env: environment,
  })
}

async function setupMiseHarness(
  t,
  root,
  { selectedVersion = '10.0.401', ambientHost = null } = {},
) {
  t.after(() => rm(root, { recursive: true, force: true }))
  const miseDirectory = join(root, 'mise-bin')
  const installRoot = join(root, 'mise-install')
  const selectedHost = await writeDotnetStub(installRoot, selectedVersion)
  await writeMiseStub(miseDirectory)
  const leading = [miseDirectory]
  if (ambientHost) leading.push(dirname(ambientHost))
  return {
    ...baseEnvironment(),
    HOME: join(root, 'home'),
    MISE_TEST_DOTNET_HOST: selectedHost,
    MISE_TEST_DOTNET_ROOT: installRoot,
    PATH: await isolatedPath(t, leading),
  }
}

test('selects and runs the exact .NET SDK from mise even when PATH has another dotnet', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-mise-selected-'))
  const ambientHost = await writeDotnetStub(join(root, 'ambient-bin'), '10.0.401')
  const environment = await setupMiseHarness(t, root, { ambientHost })
  const result = await invokeHarness(environment)

  assert.deepEqual(result.stdout.trim().split('\n'), [
    environment.MISE_TEST_DOTNET_HOST,
    await realpath(environment.MISE_TEST_DOTNET_ROOT),
  ])
  assert.equal(result.stderr, '')
})

test('binds DOTNET_ROOT to mise installation when mise returns a symlinked host', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-mise-symlink-'))
  const environment = await setupMiseHarness(t, root)
  const symlinkRoot = join(root, 'mise-bin')
  const link = join(symlinkRoot, 'selected-dotnet')
  await symlink(environment.MISE_TEST_DOTNET_HOST, link)
  environment.MISE_TEST_DOTNET_HOST = link
  const result = await invokeHarness(environment)

  assert.deepEqual(result.stdout.trim().split('\n'), [
    await realpath(link),
    await realpath(environment.MISE_TEST_DOTNET_ROOT),
  ])
})

test('rejects a mise host outside its declared installation instead of using ambient dotnet', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-mise-mismatch-'))
  const environment = await setupMiseHarness(t, root)
  const outsideHost = await writeDotnetStub(join(root, 'outside-install'), '10.0.401')
  environment.MISE_TEST_DOTNET_HOST = outsideHost
  await assert.rejects(
    () => invokeHarness(environment),
    error => {
      assert.equal(error.code, 1)
      assert.match(error.stderr, /mise selected \.NET outside its pinned installation root/u)
      return true
    },
  )
})

test('fails clearly when mise is missing even if an ambient dotnet host exists', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-mise-missing-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const ambientHost = await writeDotnetStub(join(root, 'ambient-bin'), '10.0.401')
  const path = await isolatedPath(t, [dirname(ambientHost)])
  await assert.rejects(
    () =>
      invokeHarness({
        ...baseEnvironment(),
        HOME: join(root, 'home'),
        PATH: path,
      }),
    error => {
      assert.equal(error.code, 127)
      assert.match(error.stderr, /mise is required to select the repository \.NET SDK/u)
      return true
    },
  )
})

test('--exec dotnet runs the mise-selected host directly', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-mise-exec-'))
  const environment = await setupMiseHarness(t, root)
  const result = await invokeHarness(environment, ['dotnet', '--version'])

  assert.equal(result.stdout.trim(), '10.0.401')
  assert.equal(result.stderr, '')
})

async function setupCheckHarness(t, { restoreStatus = 0, formatStatus = 0 } = {}) {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-checks-'))
  const environment = await setupMiseHarness(t, root)
  const tooling = join(root, 'dotnet-clients/tooling')
  await mkdir(tooling, { recursive: true })
  const entrypoint = join(tooling, 'harness.sh')
  await writeFile(entrypoint, await readFile(harnessPath))
  await writeFile(join(root, 'global.json'), await readFile(join(repositoryRoot, 'global.json')))
  const generated = join(root, 'dotnet-clients/src/Voucha.Client.Core/Localization/Generated')
  await mkdir(generated, { recursive: true })
  await writeFile(join(generated, 'UiMessageKey.g.cs'), '')
  for (const name of ['resx-apostrophe-path.sh', 'with-build-lock.sh']) {
    await writeExecutable(join(tooling, name), '#!/bin/bash\nexit 0\n')
  }
  await writeExecutable(
    join(root, 'mise-bin/pnpm'),
    `#!/bin/bash
while [[ "$1" != -- ]]; do shift; done
shift
exec "$@"
`,
  )
  await writeExecutable(
    environment.MISE_TEST_DOTNET_HOST,
    `#!/bin/bash
case "$1" in
  --version) echo 10.0.401 ;;
  restore)
    echo restore >> "$HARNESS_TEST_LOG"
    if [[ "$HARNESS_RESTORE_STATUS" != 0 ]]; then echo 'restore dependency unavailable' >&2; exit "$HARNESS_RESTORE_STATUS"; fi
    touch "$HARNESS_TEST_ASSETS"
    ;;
  format)
    echo format >> "$HARNESS_TEST_LOG"
    [[ -f "$HARNESS_TEST_ASSETS" ]] || { echo 'missing restore assets' >&2; exit 42; }
    [[ " $* " == *' --verify-no-changes '* && " $* " == *' --no-restore '* ]] || exit 43
    exit "$HARNESS_FORMAT_STATUS"
    ;;
  *) exit 44 ;;
esac
`,
  )
  // The fake SDK models an empty worktree without invoking a native compiler.
  environment.PATH = await isolatedPath(t, [join(root, 'mise-bin')])
  const touch = ['/usr/bin/touch', '/bin/touch'].find(existsSync)
  await symlink(touch, join(root, 'mise-bin/touch'))
  Object.assign(environment, {
    HARNESS_TEST_LOG: join(root, 'commands.log'),
    HARNESS_TEST_ASSETS: join(root, 'project.assets.json'),
    HARNESS_RESTORE_STATUS: String(restoreStatus),
    HARNESS_FORMAT_STATUS: String(formatStatus),
  })
  return {
    run: args => execFileAsync('/bin/bash', [entrypoint, ...args], { cwd: root, env: environment }),
    commands: async () => (await readFile(environment.HARNESS_TEST_LOG, 'utf8')).trim().split('\n'),
  }
}

for (const args of [[], ['--checks', 'fmt'], ['--checks', 'fmt,restore']]) {
  test(`restores fresh assets once before formatting for ${args.join(' ') || 'default checks'}`, async t => {
    const fixture = await setupCheckHarness(t)
    const result = await fixture.run(args)
    assert.deepEqual(await fixture.commands(), ['restore', 'format'])
    assert.match(result.stdout, /failed: 0/u)
  })
}

for (const args of [[], ['--checks', 'fmt']]) {
  test(`failed restore preserves diagnostics and skips formatting for ${args.join(' ') || 'default checks'}`, async t => {
    const fixture = await setupCheckHarness(t, { restoreStatus: 17 })
    await assert.rejects(
      () => fixture.run(args),
      error => {
        assert.equal(error.code, 1)
        assert.match(error.stdout, /restore \| check-failure \| 17 \|/u)
        assert.match(error.stdout, /restore dependency unavailable/u)
        assert.match(error.stdout, /fmt.*skipped.*restore/u)
        assert.doesNotMatch(error.stdout, /missing restore assets/u)
        return true
      },
    )
    assert.deepEqual(await fixture.commands(), ['restore'])
  })
}

test('formatting failure remains fatal after successful restore', async t => {
  const fixture = await setupCheckHarness(t, { formatStatus: 19 })
  await assert.rejects(
    () => fixture.run(['--checks', 'fmt']),
    error => {
      assert.equal(error.code, 1)
      assert.match(error.stdout, /fmt \| check-failure \| 19 \|/u)
      return true
    },
  )
  assert.deepEqual(await fixture.commands(), ['restore', 'format'])
})
