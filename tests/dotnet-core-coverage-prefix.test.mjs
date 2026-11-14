import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { copyFile, mkdir, mkdtemp, readlink, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { promisify } from 'node:util'
import test from 'node:test'

const execFileAsync = promisify(execFile)
const repositoryRoot = resolve(import.meta.dirname, '..')
const coverageCli = join(repositoryRoot, 'node_modules/coverage-check/bin/coverage-check.mjs')
const corePath = 'dotnet-clients/src/Voucha.Client.Core/CoverageFixture.cs'
const appPath = 'dotnet-clients/src/Voucha.Client.App/CoverageFixture.cs'
const docsPath = 'docs/coverage-fixture.md'

async function coverageFixture(t, { changedPath = corePath } = {}) {
  const parent = await mkdtemp(join(tmpdir(), 'voucha-coverage-prefix-'))
  t.after(() => rm(parent, { recursive: true, force: true }))
  const candidate = join(parent, 'candidate-clients')
  const sourcePath = join(candidate, changedPath)
  await mkdir(join(candidate, 'dotnet-clients/TestResults/core'), { recursive: true })
  for (const name of ['package.json', 'pnpm-workspace.yaml', 'pnpm-lock.yaml']) {
    await copyFile(join(repositoryRoot, name), join(candidate, name))
  }
  await copyFile(
    join(repositoryRoot, '.coverage-rules.yml'),
    join(candidate, '.coverage-rules.yml'),
  )
  // Shared node_modules retains the original workspace identity; give pnpm an isolated install
  // so verifyDepsBeforeRun stays enforced without rewriting repository dependency links.
  await execFileAsync('pnpm', ['install', '--offline', '--frozen-lockfile', '--ignore-scripts'], {
    cwd: candidate,
    timeout: 120_000,
  })
  await mkdir(join(sourcePath, '..'), { recursive: true })
  await writeFile(
    sourcePath,
    'namespace CoverageFixture;\npublic static class Sample\n{\n    public static int Value => 1;\n}\n',
  )
  const git = async (...args) => execFileAsync('git', args, { cwd: candidate })
  await git('init', '--quiet', '-b', 'main')
  await git('add', changedPath)
  await git(
    '-c',
    'user.name=Coverage Fixture',
    '-c',
    'user.email=coverage@example.test',
    'commit',
    '--quiet',
    '-m',
    'base',
  )
  const { stdout: baseOutput } = await git('rev-parse', 'HEAD')
  const base = baseOutput.trim()
  await writeFile(
    sourcePath,
    'namespace CoverageFixture;\npublic static class Sample\n{\n    public static int Value => 2;\n}\n',
  )
  await git('add', changedPath)
  await git(
    '-c',
    'user.name=Coverage Fixture',
    '-c',
    'user.email=coverage@example.test',
    'commit',
    '--quiet',
    '-m',
    'candidate',
  )
  const { stdout: headOutput } = await git('rev-parse', 'HEAD')
  const head = headOutput.trim()
  const lcovPath = join(candidate, 'dotnet-clients/TestResults/core/lcov.info')
  const environment = {
    ...process.env,
    COVERAGE_BASE: base,
    COVERAGE_HEAD: head,
    GITHUB_WORKSPACE: parent,
    GITHUB_STEP_SUMMARY: join(parent, 'step-summary.md'),
    PWD: candidate,
  }
  return { candidate, changedPath, environment, lcovPath, parent, sourcePath }
}

async function writeCoverage(fixture, hitCount, sourcePath = fixture.sourcePath) {
  await writeFile(
    fixture.lcovPath,
    `SF:${sourcePath}\nDA:1,1\nDA:2,1\nDA:3,1\nDA:4,${hitCount}\nDA:5,1\nend_of_record\n`,
  )
}

async function runCoverage(fixture, script = 'coverage:dotnet-core') {
  const dependency = join(repositoryRoot, 'node_modules/coverage-check')
  const before = await readlink(dependency)
  try {
    const result = await execFileAsync('pnpm', ['run', script], {
      cwd: fixture.candidate,
      env: fixture.environment,
    })
    return { code: 0, ...result }
  } catch (error) {
    return { code: error.code, stdout: error.stdout, stderr: error.stderr }
  } finally {
    assert.equal(
      await readlink(dependency),
      before,
      'fixture run must preserve repository dependencies',
    )
  }
}

test('the Core gate maps nested candidate paths before enforcing patch coverage', async t => {
  const fixture = await coverageFixture(t)
  await writeCoverage(fixture, 0)

  const oldInvocation = await execFileAsync(
    process.execPath,
    [
      coverageCli,
      'check',
      '--rules',
      '.coverage-rules.yml',
      '--artifacts',
      'dotnet-clients/TestResults/core',
      '--require-artifact',
      'lcov.info',
      '--fail-on-empty',
      '--base',
      fixture.environment.COVERAGE_BASE,
      '--head',
      'HEAD',
    ],
    { cwd: fixture.candidate, env: fixture.environment },
  )
  assert.match(oldInvocation.stderr, /contributed 0 coverable lines/u)

  const correctedInvocation = await runCoverage(fixture)
  assert.notEqual(correctedInvocation.code, 0)
  assert.match(correctedInvocation.stdout, /0\.0% \(0\/1\)/u)
})

test('the Core gate accepts coverage for changed executable lines', async t => {
  const fixture = await coverageFixture(t)
  await writeCoverage(fixture, 1)

  const result = await runCoverage(fixture)
  assert.equal(result.code, 0, result.stderr)
  assert.match(result.stdout, /100\.0%/u)
  assert.doesNotMatch(result.stderr, /contributed 0 coverable lines/u)
})

test('App changes retain their zero-percent rule', async t => {
  const fixture = await coverageFixture(t, { changedPath: appPath })
  await writeCoverage(fixture, 0)

  const result = await runCoverage(fixture)
  assert.equal(result.code, 0, result.stderr)
  assert.match(result.stdout, /Voucha\.Client\.App.*0\.0%/u)
})

test('documentation-only changes pass when the report has no matching coverable lines', async t => {
  const fixture = await coverageFixture(t, { changedPath: docsPath })
  await writeCoverage(fixture, 1, join(fixture.parent, 'unrelated.cs'))

  const result = await runCoverage(fixture)
  assert.equal(result.code, 0, result.stderr)
  assert.match(result.stdout, /coverage-check: PASSED/u)
})
