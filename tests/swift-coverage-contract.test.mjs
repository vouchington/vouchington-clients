import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join, relative, resolve } from 'node:path'
import { promisify } from 'node:util'
import test from 'node:test'

const execFileAsync = promisify(execFile)
const repositoryRoot = resolve(import.meta.dirname, '..')
const writeLcov = join(repositoryRoot, 'swift-clients/tooling/write-lcov.sh')
const swiftCoverage = join(repositoryRoot, 'scripts/swift-coverage.mjs')

async function writeExecutable(path, contents) {
  await writeFile(path, contents)
  await chmod(path, 0o755)
}

async function lcovFixture(t, { isolatedRepositoryRoot = false } = {}) {
  const fixtureRoot = await mkdtemp(
    join(
      isolatedRepositoryRoot ? tmpdir() : repositoryRoot,
      isolatedRepositoryRoot ? 'voucha-[root]&|*?\\\\.-' : '.write-lcov-test-',
    ),
  )
  const binDirectory = await mkdtemp(join(tmpdir(), 'voucha-xcrun-'))
  const packageDirectory = join(fixtureRoot, 'package')
  const packageBuildDirectory = join(packageDirectory, '.build')
  const buildDirectory = join(packageBuildDirectory, 'debug')
  const bundleName = 'VouchaTests'
  const binary = join(buildDirectory, `${bundleName}.xctest/Contents/MacOS/${bundleName}`)
  const argumentsPath = join(fixtureRoot, 'xcrun-arguments.txt')
  const lcovPath = join(fixtureRoot, 'llvm-cov.lcov')
  const fixtureWriteLcov = isolatedRepositoryRoot
    ? join(fixtureRoot, 'swift-clients/tooling/write-lcov.sh')
    : writeLcov
  const sourceRoot = isolatedRepositoryRoot ? fixtureRoot : repositoryRoot
  if (isolatedRepositoryRoot) {
    await mkdir(dirname(fixtureWriteLcov), { recursive: true })
    await writeExecutable(fixtureWriteLcov, await readFile(writeLcov, 'utf8'))
  }
  await mkdir(dirname(binary), { recursive: true })
  await writeFile(join(buildDirectory, 'default.profdata'), 'profile')
  await writeExecutable(binary, '#!/usr/bin/env bash\nexit 0\n')
  await writeFile(
    lcovPath,
    `SF:${sourceRoot}/swift-clients/core/Sources/VouchaCore/Example.swift\nDA:1,1\nend_of_record\n`,
  )
  await writeExecutable(
    join(binDirectory, 'xcrun'),
    '#!/usr/bin/env bash\nprintf \'%s\\n\' "$@" > "$XCRUN_ARGUMENTS_PATH"\ncat "$LCOV_FIXTURE_PATH"\n',
  )
  t.after(() =>
    Promise.all([
      rm(fixtureRoot, { recursive: true, force: true }),
      rm(binDirectory, { recursive: true, force: true }),
    ]),
  )
  return {
    argumentsPath,
    environment: {
      ...process.env,
      LCOV_FIXTURE_PATH: lcovPath,
      PATH: `${binDirectory}:${process.env.PATH}`,
      XCRUN_ARGUMENTS_PATH: argumentsPath,
    },
    fixtureRoot,
    buildDirectory,
    packageBuildDirectory,
    packagePath: isolatedRepositoryRoot ? 'package' : relative(repositoryRoot, packageDirectory),
    bundleName,
    writeLcov: fixtureWriteLcov,
  }
}

test('writes normalized LCOV with the intended .build ignore regex', async t => {
  const fixture = await lcovFixture(t)
  const outputPath = join(fixture.fixtureRoot, 'coverage/lcov.info')
  await execFileAsync(
    'bash',
    [fixture.writeLcov, fixture.packagePath, fixture.bundleName, outputPath],
    {
      cwd: repositoryRoot,
      env: fixture.environment,
    },
  )
  assert.equal(
    await readFile(outputPath, 'utf8'),
    'SF:swift-clients/core/Sources/VouchaCore/Example.swift\nDA:1,1\nend_of_record\n',
  )
  assert.deepEqual((await readFile(fixture.argumentsPath, 'utf8')).trim().split('\n'), [
    'llvm-cov',
    'export',
    '-format=lcov',
    join(
      repositoryRoot,
      fixture.packagePath,
      '.build/debug/VouchaTests.xctest/Contents/MacOS/VouchaTests',
    ),
    '-instr-profile',
    join(repositoryRoot, fixture.packagePath, '.build/debug/default.profdata'),
    '-ignore-filename-regex=\\.build',
  ])
})

test('exports mise-built core coverage with an explicitly selected matching llvm-cov', async t => {
  const fixture = await lcovFixture(t)
  const outputPath = join(fixture.fixtureRoot, 'coverage/mise-lcov.info')
  const llvmCov = join(fixture.fixtureRoot, 'mise/swift/usr/bin/llvm-cov')
  const argumentsPath = join(fixture.fixtureRoot, 'mise-llvm-cov-arguments.txt')
  await mkdir(dirname(llvmCov), { recursive: true })
  await writeExecutable(
    llvmCov,
    '#!/usr/bin/env bash\nprintf \'%s\\n\' "$@" > "$MANAGED_LLVM_COV_ARGUMENTS_PATH"\ncat "$LCOV_FIXTURE_PATH"\n',
  )
  await execFileAsync(
    'bash',
    [fixture.writeLcov, fixture.packagePath, fixture.bundleName, outputPath, llvmCov],
    {
      cwd: repositoryRoot,
      env: { ...fixture.environment, MANAGED_LLVM_COV_ARGUMENTS_PATH: argumentsPath },
    },
  )
  assert.equal(
    await readFile(outputPath, 'utf8'),
    'SF:swift-clients/core/Sources/VouchaCore/Example.swift\nDA:1,1\nend_of_record\n',
  )
  assert.equal((await readFile(argumentsPath, 'utf8')).split('\n')[0], 'export')
  await assert.rejects(readFile(fixture.argumentsPath, 'utf8'), { code: 'ENOENT' })
})

test('normalizes source paths literally when the repository root contains metacharacters', async t => {
  const fixture = await lcovFixture(t, { isolatedRepositoryRoot: true })
  const outputPath = join(fixture.fixtureRoot, 'coverage/lcov.info')
  await execFileAsync(
    'bash',
    [fixture.writeLcov, fixture.packagePath, fixture.bundleName, outputPath],
    {
      cwd: fixture.fixtureRoot,
      env: fixture.environment,
    },
  )
  assert.equal(
    await readFile(outputPath, 'utf8'),
    'SF:swift-clients/core/Sources/VouchaCore/Example.swift\nDA:1,1\nend_of_record\n',
  )
})

test('fails LCOV export when profile discovery is ambiguous', async t => {
  const fixture = await lcovFixture(t)
  await mkdir(join(fixture.fixtureRoot, 'package/.build/release'), { recursive: true })
  await writeFile(join(fixture.fixtureRoot, 'package/.build/release/default.profdata'), 'profile')
  await assert.rejects(
    execFileAsync(
      'bash',
      [fixture.writeLcov, fixture.packagePath, fixture.bundleName, 'coverage/lcov.info'],
      {
        cwd: repositoryRoot,
        env: fixture.environment,
      },
    ),
    /Expected exactly one default\.profdata profile, found 2\./,
  )
})

test('fails LCOV export clearly when the package build directory is missing', async t => {
  const fixture = await lcovFixture(t)
  await rm(fixture.packageBuildDirectory, { recursive: true, force: true })
  await assert.rejects(
    execFileAsync(
      'bash',
      [fixture.writeLcov, fixture.packagePath, fixture.bundleName, 'coverage/lcov.info'],
      {
        cwd: repositoryRoot,
        env: fixture.environment,
      },
    ),
    /Expected Swift package build directory at .+\/package\/\.build\./,
  )
})

test('requires a full pull-request base SHA before invoking coverage-check', async () => {
  await assert.rejects(
    execFileAsync(process.execPath, [swiftCoverage], {
      cwd: repositoryRoot,
      env: { ...process.env, BASE_SHA: 'not-a-sha' },
    }),
    /BASE_SHA must be the 40-character pull-request base SHA/,
  )
})

test('requires the pull-request base SHA to identify a repository commit', async () => {
  await assert.rejects(
    execFileAsync(process.execPath, [swiftCoverage], {
      cwd: repositoryRoot,
      env: { ...process.env, BASE_SHA: '0'.repeat(40) },
    }),
    /BASE_SHA must identify a commit in the repository/,
  )
})

test('invokes coverage-check with both Swift LCOV artifacts', async t => {
  const binDirectory = await mkdtemp(join(tmpdir(), 'voucha-pnpm-'))
  const argumentsPath = join(binDirectory, 'pnpm-arguments.txt')
  await writeExecutable(
    join(binDirectory, 'pnpm'),
    '#!/usr/bin/env bash\nprintf \'%s\\n\' "$@" > "$PNPM_ARGUMENTS_PATH"\n',
  )
  t.after(() => rm(binDirectory, { recursive: true, force: true }))
  const { stdout: baseShaOutput } = await execFileAsync('git', ['rev-parse', 'HEAD'], {
    cwd: repositoryRoot,
  })
  const baseSha = baseShaOutput.trim()
  await execFileAsync(process.execPath, [swiftCoverage], {
    cwd: repositoryRoot,
    env: {
      ...process.env,
      BASE_SHA: baseSha,
      PATH: `${binDirectory}:${process.env.PATH}`,
      PNPM_ARGUMENTS_PATH: argumentsPath,
    },
  })
  assert.deepEqual((await readFile(argumentsPath, 'utf8')).trim().split('\n'), [
    'exec',
    'coverage-check',
    'check',
    '--rules',
    '.coverage-rules.yml',
    '--artifacts',
    'coverage',
    '--require-artifact',
    'core/lcov.info',
    '--require-artifact',
    'ui/lcov.info',
    '--base',
    baseSha,
    '--head',
    'HEAD',
    '--aggregate-artifacts',
    '--fail-on-empty',
    '--annotate-source',
  ])
})
