import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { existsSync } from 'node:fs'
import { chmod, mkdir, mkdtemp, realpath, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { promisify } from 'node:util'
import test from 'node:test'

const execFileAsync = promisify(execFile)
const repositoryRoot = resolve(import.meta.dirname, '..')
const harnessPath = join(repositoryRoot, 'dotnet-clients/tooling/harness.sh')

async function writeExecutable(path, contents) {
  await writeFile(path, contents)
  await chmod(path, 0o755)
}

function pathWithoutDotnet(leadingDirectories) {
  const inherited = (process.env.PATH || '')
    .split(':')
    .filter(Boolean)
    .filter(
      directory =>
        !existsSync(join(directory, 'dotnet')) && !existsSync(join(directory, 'dotnet.exe')),
    )
  return [...leadingDirectories, ...inherited].join(':')
}

function baseEnvironment() {
  const environment = { ...process.env }
  delete environment.DOTNET_INSTALL_DIR
  delete environment.DOTNET_MULTILEVEL_LOOKUP
  delete environment.DOTNET_ROOT
  delete environment.GITHUB_ACTIONS
  return environment
}

async function writeDotnetStub(directory, { status, message }) {
  await mkdir(directory, { recursive: true })
  const host = join(directory, 'dotnet')
  const quotedMessage = message.replaceAll("'", "'\\''")
  const stream = status === 0 ? '' : ' >&2'
  await writeExecutable(
    host,
    `#!/bin/bash\nprintf '%s\\n' '${quotedMessage}'${stream}\nexit ${status}\n`,
  )
  return realpath(host)
}

async function invokeHarness(environment) {
  return execFileAsync(
    'bash',
    [harnessPath, '--exec', '/bin/bash', '-c', 'printf \'%s\\n\' "$DOTNET_HOST" "$DOTNET_ROOT"'],
    {
      cwd: repositoryRoot,
      encoding: 'utf8',
      env: environment,
    },
  )
}

test('uses the first PATH host when it satisfies global.json', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-path-ok-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const pathHost = await writeDotnetStub(join(root, 'path-bin'), {
    status: 0,
    message: '10.0.301',
  })
  const fallbackHost = await writeDotnetStub(join(root, 'home/.dotnet'), {
    status: 0,
    message: '10.0.999',
  })
  const result = await invokeHarness({
    ...baseEnvironment(),
    HOME: join(root, 'home'),
    PATH: pathWithoutDotnet([join(root, 'path-bin')]),
  })
  assert.deepEqual(result.stdout.trim().split('\n'), [pathHost, dirname(pathHost)])
  assert.notEqual(result.stdout.trim().split('\n')[0], fallbackHost)
})

test('falls back to the Microsoft user-local host when PATH-first fails locally', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-home-fallback-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  await writeDotnetStub(join(root, 'path-bin'), {
    status: 155,
    message: 'Voucha requires a compatible .NET 10.0.3xx SDK.',
  })
  const fallbackHost = await writeDotnetStub(join(root, 'home/.dotnet'), {
    status: 0,
    message: '10.0.301',
  })
  const result = await invokeHarness({
    ...baseEnvironment(),
    HOME: join(root, 'home'),
    PATH: pathWithoutDotnet([join(root, 'path-bin')]),
  })
  assert.deepEqual(result.stdout.trim().split('\n'), [fallbackHost, dirname(fallbackHost)])
})

test('does not search HOME/.dotnet when GITHUB_ACTIONS is set', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-ci-isolated-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const pathHost = await writeDotnetStub(join(root, 'path-bin'), {
    status: 155,
    message: 'Voucha requires a compatible .NET 10.0.3xx SDK.',
  })
  const fallbackHost = await writeDotnetStub(join(root, 'home/.dotnet'), {
    status: 0,
    message: '10.0.301',
  })
  await assert.rejects(
    () =>
      invokeHarness({
        ...baseEnvironment(),
        DOTNET_ROOT: join(root, 'home/.dotnet'),
        GITHUB_ACTIONS: 'true',
        HOME: join(root, 'home'),
        PATH: pathWithoutDotnet([join(root, 'path-bin')]),
      }),
    error => {
      assert.equal(error.code, 155)
      assert.ok(error.stdout.includes(`dotnet host: ${pathHost} (exit 155)`))
      assert.equal(error.stdout.includes(fallbackHost), false)
      assert.match(
        error.stderr,
        /The PATH-selected dotnet host cannot satisfy the repository root global\.json policy/u,
      )
      return true
    },
  )
})

test('prefers an explicit DOTNET_ROOT host over HOME/.dotnet locally', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-root-fallback-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  await writeDotnetStub(join(root, 'path-bin'), {
    status: 155,
    message: 'Voucha requires a compatible .NET 10.0.3xx SDK.',
  })
  const rootHost = await writeDotnetStub(join(root, 'explicit-root'), {
    status: 0,
    message: '10.0.301',
  })
  const homeHost = await writeDotnetStub(join(root, 'home/.dotnet'), {
    status: 0,
    message: '10.0.302',
  })
  const result = await invokeHarness({
    ...baseEnvironment(),
    DOTNET_ROOT: join(root, 'explicit-root'),
    HOME: join(root, 'home'),
    PATH: pathWithoutDotnet([join(root, 'path-bin')]),
  })
  assert.deepEqual(result.stdout.trim().split('\n'), [rootHost, dirname(rootHost)])
  assert.notEqual(result.stdout.trim().split('\n')[0], homeHost)
})

test('falls back to HOME/.dotnet when PATH has no dotnet host', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-home-only-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  await mkdir(join(root, 'empty-bin'), { recursive: true })
  const fallbackHost = await writeDotnetStub(join(root, 'home/.dotnet'), {
    status: 0,
    message: '10.0.301',
  })
  const result = await invokeHarness({
    ...baseEnvironment(),
    HOME: join(root, 'home'),
    PATH: pathWithoutDotnet([join(root, 'empty-bin')]),
  })
  assert.deepEqual(result.stdout.trim().split('\n'), [fallbackHost, dirname(fallbackHost)])
})

test('exits 127 when no PATH host or local fallback exists', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-missing-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  await mkdir(join(root, 'empty-bin'), { recursive: true })
  await mkdir(join(root, 'home'), { recursive: true })
  await assert.rejects(
    () =>
      invokeHarness({
        ...baseEnvironment(),
        HOME: join(root, 'home'),
        PATH: pathWithoutDotnet([join(root, 'empty-bin')]),
      }),
    error => {
      assert.equal(error.code, 127)
      assert.match(error.stderr, /Error: dotnet was not found on PATH/u)
      return true
    },
  )
})

test('replays every failed candidate host path before giving up', async t => {
  const root = await mkdtemp(join(tmpdir(), 'voucha-dotnet-all-fail-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const pathHost = await writeDotnetStub(join(root, 'path-bin'), {
    status: 155,
    message: 'PATH host cannot satisfy global.json',
  })
  const homeHost = await writeDotnetStub(join(root, 'home/.dotnet'), {
    status: 155,
    message: 'HOME host cannot satisfy global.json',
  })
  await assert.rejects(
    () =>
      invokeHarness({
        ...baseEnvironment(),
        HOME: join(root, 'home'),
        PATH: pathWithoutDotnet([join(root, 'path-bin')]),
      }),
    error => {
      assert.equal(error.code, 155)
      assert.ok(error.stdout.includes(`dotnet host: ${pathHost} (exit 155)`))
      assert.ok(error.stdout.includes(`dotnet host: ${homeHost} (exit 155)`))
      assert.match(error.stdout, /PATH host cannot satisfy global\.json/u)
      assert.match(error.stdout, /HOME host cannot satisfy global\.json/u)
      return true
    },
  )
})
