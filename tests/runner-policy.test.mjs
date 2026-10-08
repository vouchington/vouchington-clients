import assert from 'node:assert/strict'
import { readdir, readFile } from 'node:fs/promises'
import { join, relative, resolve } from 'node:path'
import { describe, it } from 'node:test'

const repositoryRoot = resolve(import.meta.dirname, '..')
const githubRoot = join(repositoryRoot, '.github')
const workflowRoot = join(githubRoot, 'workflows')
const allowedLabels = new Set(['ubuntu-slim', 'ubuntu-latest', 'macos-latest'])

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

describe('GitHub-hosted runner policy', () => {
  it('rejects self-hosted labels and unlisted hosted images', async () => {
    for (const [name, workflow] of await readWorkflows()) {
      assert.doesNotMatch(workflow, /\bself-hosted\b/u, `${name} selects a self-hosted runner`)
      const labels = [...workflow.matchAll(/^\s+runs-on:\s*(.+)$/gmu)].map(match => match[1].trim())
      assert.ok(labels.length > 0, `${name} must declare runs-on`)
      for (const label of labels) {
        assert.ok(allowedLabels.has(label), `${name} uses disallowed runs-on ${label}`)
      }
    }
  })

  it('maps former self-hosted jobs onto the closed hosted allowlist', async () => {
    const workflows = Object.fromEntries(await readWorkflows())
    for (const [job, runner] of [
      ['contract-tests', 'ubuntu-latest'],
      ['dotnet-core', 'ubuntu-latest'],
      ['swift-core', 'ubuntu-latest'],
      ['swift-lint', 'ubuntu-latest'],
      ['tooling-lint', 'ubuntu-latest'],
      ['gitleaks', 'ubuntu-slim'],
      ['validate', 'ubuntu-slim'],
    ])
      assertRunner(workflows['validate.yml'], job, runner)
    assertRunner(workflows['native-contract-tests.yml'], 'produce', 'ubuntu-latest')
    assertRunner(workflows['native-contract-tests.yml'], 'verify', 'ubuntu-latest')
    assertRunner(workflows['native-contract-tests.yml'], 'tests', 'ubuntu-slim')
    assertRunner(workflows['native-contract-tests.yml'], 'native-dto-fixture-parity', 'ubuntu-slim')
    assertRunner(workflows['native-contract-tests.yml'], 'dotnet-portable', 'ubuntu-latest')
    assertRunner(workflows['native-contract-tests.yml'], 'test-swift-core', 'macos-latest')
    assertRunner(workflows['dependabot-automerge.yml'], 'automerge', 'ubuntu-slim')
    assertRunner(workflows['dependabot-automerge.yml'], 'prepare', 'ubuntu-slim')
    assertRunner(workflows['dependabot-automerge.yml'], 'publish-swift-android', 'ubuntu-slim')
    assertRunner(workflows['repair-dependabot-dotnet-locks.yml'], 'prepare', 'macos-latest')
    assertRunner(workflows['repair-dependabot-dotnet-locks.yml'], 'publish', 'ubuntu-slim')

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

  it('does not run persistent-workspace cleanup or shared-host isolation', async () => {
    for (const [name, source] of await readGithubYamlFiles()) {
      assert.doesNotMatch(source, /clean-workspace/u, `${name} still cleans a persistent workspace`)
      assert.doesNotMatch(source, /with-host-lock/u, `${name} still acquires a host lock`)
      assert.doesNotMatch(
        source,
        /with-build-lock/u,
        `${name} still wraps a shared-host build lock`,
      )
      assert.doesNotMatch(
        source,
        /MISE_DATA_DIR/u,
        `${name} still jails mise against a shared home`,
      )
      assert.doesNotMatch(source, /clean: false/u, `${name} still preserves a persistent checkout`)
    }
  })
})
