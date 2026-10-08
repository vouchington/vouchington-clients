import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { existsSync } from 'node:fs'
import { chmod, mkdir, mkdtemp, realpath, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { promisify } from 'node:util'
import test from 'node:test'

const execFileAsync = promisify(execFile)
const repositoryRoot = resolve(import.meta.dirname, '..')
const harnessPath = join(repositoryRoot, 'dotnet-clients/tooling/harness.sh')
const harnessTools = ['cat', 'dirname', 'env', 'mktemp', 'node', 'realpath', 'rm', 'tr', 'whoami']

async function writeExecutable(path, contents) {
  await writeFile(path, contents)
  await chmod(path, 0o755)
}

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
