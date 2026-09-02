import assert from 'node:assert/strict'
import { readdir, readFile } from 'node:fs/promises'
import { join, relative, resolve } from 'node:path'
import { describe, it } from 'node:test'

const repositoryRoot = resolve(import.meta.dirname, '..')
const githubRoot = join(repositoryRoot, '.github')
const workflowRoot = join(githubRoot, 'workflows')

async function readWorkflows() {
  const names = (await readdir(workflowRoot)).filter(name => name.endsWith('.yml')).sort()
  return Promise.all(
    names.map(async name => [name, await readFile(join(workflowRoot, name), 'utf8')]),
  )
}

async function readGithubYamlFiles(directory = githubRoot) {
  const entries = await readdir(directory, { withFileTypes: true })
  const files = await Promise.all(
    entries.map(async entry => {
      const fullPath = join(directory, entry.name)
      if (entry.isDirectory()) return readGithubYamlFiles(fullPath)
      if (!entry.name.endsWith('.yml') && !entry.name.endsWith('.yaml')) return []
      return [[relative(repositoryRoot, fullPath), await readFile(fullPath, 'utf8')]]
    }),
  )
  return files.flat()
}

function jobBlock(workflow, job) {
  const start = workflow.indexOf(`  ${job}:`)
  assert.notEqual(start, -1, `missing job ${job}`)
  const remainder = workflow.slice(start)
  const nextJob = remainder.search(/\n {2}[A-Za-z0-9_-]+:/u)
  return remainder.slice(0, nextJob === -1 ? remainder.length : nextJob)
}

function assertRunner(workflow, job, runner) {
  assert.match(jobBlock(workflow, job), new RegExp(`runs-on: ${runner}`, 'u'))
}

function jobUnits(source) {
  const jobsAt = source.search(/^jobs:/mu)
  if (jobsAt === -1) return [source]
  const jobs = source.slice(jobsAt)
  const starts = [...jobs.matchAll(/^ {2}[A-Za-z0-9_-]+:/gmu)].map(match => match.index)
  if (starts.length === 0) return [source]
  return starts.map((start, i) => jobs.slice(start, starts[i + 1]))
}

function matchOffsets(source, pattern) {
  return [...source.matchAll(pattern)].map(match => match.index)
}

function assertMiseIsolation(name, source) {
  const units = jobUnits(source).filter(unit => unit.includes('jdx/mise-action@'))
  assert.ok(units.length > 0, `${name} must contain mise-action`)
  for (const unit of units) {
    const actions = matchOffsets(unit, /jdx\/mise-action@/gu)
    const setups = matchOffsets(unit, /mise_root="\$RUNNER_TEMP\/mise"/gu)
    const dirs = matchOffsets(unit, /mise_dir: \$\{\{ runner\.temp \}\}\/mise/gu)
    assert.equal(
      setups.length,
      actions.length,
      `${name} must isolate MISE_DATA_DIR once per mise-action in the same job`,
    )
    assert.equal(
      dirs.length,
      actions.length,
      `${name} must point every mise-action at RUNNER_TEMP in the same job`,
    )
    actions.forEach((actionAt, index) => {
      assert.ok(
        setups[index] < actionAt,
        `${name} must set MISE_DATA_DIR before mise-action ${index}`,
      )
      assert.ok(dirs[index] > actionAt, `${name} must set mise_dir on mise-action ${index}`)
      assert.ok(
        index === actions.length - 1 || dirs[index] < actions[index + 1],
        `${name} mise_dir ${index} must belong to mise-action ${index}`,
      )
      assert.ok(
        index === actions.length - 1 || setups[index + 1] > actionAt,
        `${name} must not reuse a later job's mise isolation`,
      )
    })
    assert.match(unit, /printf 'MISE_DATA_DIR=%s\\n' "\$mise_root" >> "\$GITHUB_ENV"/u)
  }
  assert.doesNotMatch(
    source,
    /~\/\.local\/share\/mise/u,
    `${name} must not hardcode the host mise path`,
  )
}

function assertPersistentCleanup(workflow, job, requireTempCleanup = false) {
  const block = jobBlock(workflow, job)
  const checkout = block.indexOf('actions/checkout@')
  const cleanup = [...block.matchAll(/name: Clean persistent runner workspace/gu)].map(
    match => match.index,
  )
  assert.notEqual(checkout, -1, `${job} must check out code`)
  assert.match(block, /clean: false/u, `${job} must preserve checkout-managed dependencies`)
  assert.match(block, /persist-credentials: false/u, `${job} must not persist credentials`)
  assert.ok(cleanup.length >= 2, `${job} needs pre- and post-job cleanup`)
  assert.ok(cleanup[0] < checkout, `${job} needs cleanup before checkout`)
  assert.ok(cleanup.at(-1) > checkout, `${job} needs cleanup after checkout`)
  assert.match(block, /PRESERVE_NODE_MODULES: ["']false["']/u)
  assert.match(block, /pnpm dlx vouchington-tooling@0\.1\.5 clean-workspace/u)
  if (requireTempCleanup) {
    assert.equal(
      [...block.matchAll(/working-directory: \$\{\{ runner\.temp \}\}/gu)].length,
      cleanup.length,
      `${job} must run every persistent-workspace cleanup outside the checkout`,
    )
  }
  assert.match(block, /if: always\(\)/u, `${job} needs unconditional final cleanup`)
}

describe('private self-hosted runner policy', () => {
  it('rejects GitHub-hosted runner labels in every workflow', async () => {
    for (const [name, workflow] of await readWorkflows()) {
      assert.doesNotMatch(
        workflow,
        /\b(?:ubuntu|macos|windows)-(?:latest|slim|\d[\w.-]*)\b/iu,
        `${name} selects a GitHub-hosted runner`,
      )
    }
  })

  it('uses the exact self-hosted labels for the former hosted jobs', async () => {
    const workflows = Object.fromEntries(await readWorkflows())
    for (const [job, runner] of [
      ['contract-tests', '\\[self-hosted, Linux\\]'],
      ['dotnet-core', '\\[self-hosted, Linux\\]'],
      ['swift-core', '\\[self-hosted, Linux, Docker, Tests\\]'],
      ['swift-lint', '\\[self-hosted, Linux, Docker, Tests\\]'],
      ['tooling-lint', '\\[self-hosted, Linux\\]'],
      ['gitleaks', '\\[self-hosted, Linux\\]'],
      ['validate', '\\[self-hosted, Linux\\]'],
    ])
      assertRunner(workflows['validate.yml'], job, runner)
    assertRunner(workflows['native-contract-tests.yml'], 'produce', '\\[self-hosted, Linux\\]')
    assertRunner(workflows['native-contract-tests.yml'], 'verify', '\\[self-hosted, Linux\\]')
    assertRunner(workflows['native-contract-tests.yml'], 'tests', '\\[self-hosted, Linux\\]')
    assertRunner(workflows['dependabot-automerge.yml'], 'automerge', '\\[self-hosted, Linux\\]')
    assertRunner(workflows['dependabot-automerge.yml'], 'prepare', '\\[self-hosted, Linux\\]')
    assertRunner(
      workflows['dependabot-automerge.yml'],
      'publish-swift-android',
      '\\[self-hosted, Linux\\]',
    )
    assertRunner(
      workflows['repair-dependabot-dotnet-locks.yml'],
      'prepare',
      '\\[self-hosted, macOS, Tests\\]',
    )
    assertRunner(
      workflows['repair-dependabot-dotnet-locks.yml'],
      'publish',
      '\\[self-hosted, Linux\\]',
    )

    const swiftManifest = jobBlock(workflows['validate.yml'], 'swift-core')
    assert.match(swiftManifest, /docker run --rm/u)
    assert.match(swiftManifest, /--read-only/u)
    assert.match(swiftManifest, /--network none/u)
    assert.match(swiftManifest, /--volume "\$PWD:\/workspace:ro"/u)
    assert.match(swiftManifest, /--workdir \/workspace/u)
    assert.match(swiftManifest, /swift:6\.3\.3-noble@sha256:[0-9a-f]{64}/u)
    assert.match(swiftManifest, /swift package --package-path swift-clients\/core dump-package/u)
    assert.doesNotMatch(swiftManifest, /swift (?:build|test)/u)
  })

  it('isolates mise from the shared self-hosted home directory', async () => {
    const files = await readGithubYamlFiles()
    const miseFiles = files.filter(([, source]) => source.includes('jdx/mise-action@'))
    assert.ok(miseFiles.length > 0, 'expected mise-action usages')
    for (const [name, source] of miseFiles) assertMiseIsolation(name, source)
  })

  it('rejects a mise-action job that borrows isolation from another job', () => {
    const leaked = `
jobs:
  isolated:
    steps:
      - run: |
          mise_root="$RUNNER_TEMP/mise"
          printf 'MISE_DATA_DIR=%s\\n' "$mise_root" >> "$GITHUB_ENV"
      - uses: jdx/mise-action@deadbeef
        with:
          mise_dir: \${{ runner.temp }}/mise
  unused:
    steps:
      - run: |
          mise_root="$RUNNER_TEMP/mise"
          printf 'MISE_DATA_DIR=%s\\n' "$mise_root" >> "$GITHUB_ENV"
  leaked:
    steps:
      - uses: jdx/mise-action@deadbeef
        with:
          mise_dir: \${{ runner.temp }}/mise
`
    assert.throws(
      () => assertMiseIsolation('fixture.yml', leaked),
      /must isolate MISE_DATA_DIR once per mise-action in the same job/u,
    )
  })

  it('cleans migrated and sensitive persistent-runner jobs before and after checkout', async () => {
    const workflows = Object.fromEntries(await readWorkflows())
    for (const [workflow, job, requireTempCleanup] of [
      ['validate.yml', 'contract-tests'],
      ['validate.yml', 'dotnet-core'],
      ['validate.yml', 'swift-core'],
      ['validate.yml', 'swift-lint'],
      ['validate.yml', 'tooling-lint'],
      ['validate.yml', 'gitleaks'],
      ['native-contract-tests.yml', 'verify'],
      ['native-contract-tests.yml', 'produce'],
      ['dependabot-automerge.yml', 'prepare'],
      ['dependabot-automerge.yml', 'publish-swift-android'],
      ['repair-dependabot-dotnet-locks.yml', 'prepare'],
      ['repair-dependabot-dotnet-locks.yml', 'publish'],
    ])
      assertPersistentCleanup(workflows[workflow], job, requireTempCleanup)
  })
})
