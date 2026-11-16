import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { describe, it } from 'node:test'
import { jobBlock } from './workflow-job-block.mjs'

const read = name => readFile(new URL(`../.github/workflows/${name}`, import.meta.url), 'utf8')
const [native, validate] = await Promise.all([
  read('native-contract-tests.yml'),
  read('validate.yml'),
])
const mergeGroupTrigger = /\n {2}merge_group:\n {4}types: \[checks_requested\]\n/u
const pushOnlyMain = "github.event_name == 'push' && github.ref == 'refs/heads/main'"
const jobsSection = native.slice(native.search(/^jobs:/mu))
const jobs = [...jobsSection.matchAll(/^ {2}([a-z0-9-]+):$/gmu)].map(match => match[1])
const header = name => jobBlock(native, name).split('\n    steps:')[0]
const stepBlock = (job, step) => {
  const block = jobBlock(native, job)
  const start = block.indexOf(`- name: ${step}\n`)
  assert.notEqual(start, -1, `${job}: ${step}`)
  return block.slice(start).split(/\n {6}- /u)[0]
}

// Jobs that stay on a main push because they warm a cache or upload Sentry symbols.
const warmOnPush = [
  'produce',
  'verify',
  'dotnet-portable',
  'build-swift-android',
  'test-swift-android-libs',
  'build-android-core',
  'build-macos-app',
  'build-ios-tests',
  'dotnet-maui',
  'sentry-dotnet-symbols',
  'dotnet-catalyst-gui',
  'dotnet-windows-image-preview',
  'dotnet-windows-maui',
  'sentry-dotnet-windows-symbols',
  'dotnet-windows-language-model-surface',
]

describe('merge queue CI (#705)', () => {
  it('runs validate for merge groups instead of main pushes', () => {
    assert.match(validate, mergeGroupTrigger)
    assert.doesNotMatch(validate, /^ {2}push:/mu)
    assert.doesNotMatch(validate, /github\.event\.before/u)
    assert.ok(
      validate.includes(
        'BASE_SHA: ${{ github.event.pull_request.base.sha || github.event.merge_group.base_sha }}',
      ),
    )
    assert.match(validate, /\n {2}validate:\n {4}name: validate\n/u)
    assert.match(validate, /cancel-in-progress: \$\{\{ github\.event_name == 'pull_request' \}\}/u)
  })

  it('triggers native tests for pull requests, merge groups, and main pushes', () => {
    assert.match(native, mergeGroupTrigger)
    assert.match(native, /push:\n\s+branches: \[main\]/u)
    assert.ok(native.includes("github.event_name == 'merge_group' && format('merge queue at {0}'"))
    assert.match(native, /cancel-in-progress: \$\{\{ github\.event_name == 'pull_request' \}\}/u)
  })

  it('binds merge-group identity to the queue ref and payload SHAs in produce and verify', () => {
    for (const job of ['produce', 'verify']) {
      const block = jobBlock(native, job)
      assert.ok(
        block.includes('MERGE_GROUP_BASE_SHA: ${{ github.event.merge_group.base_sha }}'),
        job,
      )
      assert.ok(
        block.includes('MERGE_GROUP_HEAD_SHA: ${{ github.event.merge_group.head_sha }}'),
        job,
      )
      assert.ok(block.includes('refs/heads/gh-readonly-queue/main/'), job)
    }
    const produce = jobBlock(native, 'produce')
    assert.match(produce, /candidate_event=merge_group\n\s+candidate_number=\n/u)
    assert.ok(produce.includes('base_sha="$MERGE_GROUP_BASE_SHA"'))
    assert.ok(produce.includes('head_sha="$MERGE_GROUP_HEAD_SHA"'))
    assert.ok(produce.includes('[ "$EVENT_SHA" = "$MERGE_GROUP_HEAD_SHA" ]'))
    assert.ok(produce.includes('Unsupported native test event'))
    const verify = jobBlock(native, 'verify')
    assert.ok(verify.includes("manifest.candidate.event !== 'merge_group'"))
    assert.ok(verify.includes('manifest.candidate.base !== process.env.MERGE_GROUP_BASE_SHA'))
    assert.ok(verify.includes('unsupported native test event'))
  })

  it('uses the merge-group base as the Gradle task cache base revision', () => {
    assert.ok(
      jobBlock(native, 'build-swift-android').includes(
        'PR_BASE_REVISION: ${{ github.event.pull_request.base.sha || github.event.merge_group.base_sha }}',
      ),
    )
  })

  it('reads github.event.before only where a main push supplies it', () => {
    const users = jobs.filter(name => jobBlock(native, name).includes('github.event.before'))
    assert.deepEqual(users, ['produce', 'verify', 'build-swift-android'])
  })

  it('skips test-only jobs on a main push and keeps only warm or Sentry jobs', () => {
    const kept = jobs.filter(name => !header(name).includes("github.event_name != 'push'"))
    assert.deepEqual([...kept].sort(), [...warmOnPush].sort())
  })

  it('does not run the Tests aggregate, DTO advisory, or Android cookie proof on a push', () => {
    for (const name of ['tests', 'native-dto-fixture-parity', 'test-swift-android-cookie']) {
      assert.ok(
        header(name).includes("if: ${{ github.event_name != 'push' && !cancelled() }}"),
        name,
      )
    }
  })

  it('gates test steps inside warm jobs off pushes', () => {
    const steps = [
      ['dotnet-portable', 'Test portable solution with coverage'],
      ['dotnet-portable', 'Enforce Core patch coverage'],
      ['test-swift-android-libs', 'Build the selected Android validation phase'],
      ['build-android-core', 'Build Android core package'],
      ['build-macos-app', 'Launch macOS root shell, sidebar feeds, and email sign-in'],
      ['build-ios-tests', 'Build selected iPhone test products'],
      ['dotnet-catalyst-gui', 'Exercise Catalyst email sign-in and News controls'],
      ['dotnet-windows-image-preview', 'Test the Windows image decoder'],
      ['dotnet-windows-language-model-surface', 'Probe the pinned Windows language-model surface'],
    ]
    for (const [job, step] of steps) {
      assert.ok(stepBlock(job, step).includes("if: ${{ github.event_name != 'push'"), step)
    }
  })

  it('saves caches and uploads Sentry symbols only from a main push, never a merge group', () => {
    const saves = native.split(/\n(?= {6}- )/u).filter(step => step.includes('actions/cache/save'))
    assert.ok(saves.length >= 7)
    for (const step of saves) {
      assert.ok(
        step.includes(pushOnlyMain) || step.includes('android-task-cache-save.outputs.save'),
        step,
      )
    }
    for (const name of ['sentry-dotnet-symbols', 'sentry-dotnet-windows-symbols']) {
      assert.ok(header(name).includes(`if: ${pushOnlyMain}`), name)
    }
    assert.doesNotMatch(native, /VOUCHA_SENTRY_[A-Z_]+: .*merge_group/u)
  })
})
