import assert from 'node:assert/strict'
import { readdir, readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import { describe, it } from 'node:test'

const repositoryRoot = resolve(import.meta.dirname, '..')
const workflowRoot = join(repositoryRoot, '.github/workflows')

async function readWorkflows() {
  const names = (await readdir(workflowRoot)).filter(name => name.endsWith('.yml')).sort()
  return Promise.all(
    names.map(async name => [name, await readFile(join(workflowRoot, name), 'utf8')]),
  )
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

  it('cleans migrated and sensitive persistent-runner jobs before and after checkout', async () => {
    const workflows = Object.fromEntries(await readWorkflows())
    for (const [workflow, job, requireTempCleanup] of [
      ['validate.yml', 'contract-tests'],
      ['validate.yml', 'dotnet-core'],
      ['validate.yml', 'swift-core'],
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
