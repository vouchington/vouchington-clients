import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { test } from 'node:test'
import { jobBlock } from './workflow-job-block.mjs'

const workflow = readFileSync(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)
const release = jobBlock(workflow, 'dotnet-maui')
const preflight = jobBlock(workflow, 'dotnet-catalyst-gui')
const aggregate = jobBlock(workflow, 'tests')

test('the Release (AOT) Mac Catalyst build runs on merge_group only', () => {
  assert.match(release, /\n {4}if: \$\{\{ github\.event_name == 'merge_group' && /u)
  assert.match(release, /--configuration Release --framework net10\.0-maccatalyst/u)
  assert.doesNotMatch(release, /--configuration Debug/u)
  assert.doesNotMatch(release, /github\.event_name == 'pull_request'/u)
})

test('the pull_request path builds Mac Catalyst as Debug without AOT', () => {
  assert.match(preflight, /\n {4}if: \$\{\{ needs\.verify\.outputs\.docs-only != 'true' \}\}/u)
  const build = preflight.slice(preflight.indexOf('- name: Build the Debug app'))
  assert.match(build, /--configuration Debug --framework net10\.0-maccatalyst/u)
  assert.match(build, /if: \$\{\{ github\.event_name != 'push' \}\}/u)
  assert.doesNotMatch(preflight, /PublishAot|--configuration Release|-p:Configuration=Release/u)
})

test('Debug and Release builds do not share a cache', () => {
  assert.doesNotMatch(release, /actions\/cache\/save/u)
  assert.doesNotMatch(preflight, /Configuration=Release|nuget-maui-release/u)
})

test('Tests accepts a skipped Release job only on pull_request', () => {
  assert.match(aggregate, /- dotnet-maui\n/u)
  const expression = workflow
    .slice(workflow.lastIndexOf('jq -e --arg event'))
    .split("'\n")[1]
    .split("\n          '")[0]
  const keys = [...aggregate.matchAll(/\n {6}- ([a-z0-9-]+)/gu)].map(m => m[1])
  const skipped = [
    'dotnet-windows-image-preview',
    'dotnet-windows-language-model-surface',
    'dotnet-windows-maui',
  ]
  const run = (event, mauiResult) => {
    const needs = Object.fromEntries(keys.map(key => [key, { result: 'success' }]))
    for (const key of skipped) needs[key] = { result: 'skipped' }
    needs['dotnet-maui'] = { result: mauiResult }
    const args = ['route', 'maui', 'surface', 'docs'].flatMap(n => ['--arg', n, 'false'])
    try {
      execFileSync('jq', ['-e', '--arg', 'event', event, ...args, expression], {
        input: JSON.stringify(needs),
        stdio: 'pipe',
      })
      return true
    } catch {
      return false
    }
  }
  assert.equal(run('pull_request', 'skipped'), true)
  assert.equal(run('pull_request', 'success'), true)
  assert.equal(run('pull_request', 'failure'), false)
  assert.equal(run('merge_group', 'success'), true)
  assert.equal(run('merge_group', 'skipped'), false)
  assert.equal(run('merge_group', 'failure'), false)
})
