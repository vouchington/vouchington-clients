import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { describe, it } from 'node:test'
import { jobBlock } from './workflow-job-block.mjs'

const workflow = await readFile(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)

describe('cross-job artifact attempts survive failed-job reruns', () => {
  it('names downloads with the uploader attempt, never the consumer attempt', () => {
    const lines = workflow.split('\n')
    let downloads = 0
    lines.forEach((line, index) => {
      if (!line.includes('actions/download-artifact@')) return
      downloads += 1
      const name = lines.slice(index, index + 8).find(item => item.trim().startsWith('name:'))
      assert.ok(name, `download at line ${index + 1} has a name`)
      assert.doesNotMatch(name, /github\.run_attempt/u)
      if (name.endsWith('name: native-contract-${{ github.run_id }}')) return
      assert.match(name, /needs\.[\w-]+\.outputs\.artifact-attempt/u)
    })
    assert.equal(downloads, 7)
    assert.doesNotMatch(
      workflow,
      /artifact: sentry-[\w-]+-\$\{\{ github\.run_id \}\}-\$\{\{ github\.run_attempt/u,
    )
    assert.doesNotMatch(workflow, /producer-run-attempt: \$\{\{ github\.run_attempt/u)
  })

  it('exports the attempt from every uploading job', () => {
    for (const name of [
      'build-swift-android',
      'build-swift-ui-tests',
      'test-swift-core',
      'test-swift-ui',
      'build-ios-tests',
      'dotnet-maui',
      'dotnet-windows-maui',
    ]) {
      assert.match(jobBlock(workflow, name), /artifact-attempt: \$\{\{ github\.run_attempt \}\}/u)
    }
    const produce = jobBlock(workflow, 'produce')
    assert.match(
      produce,
      /contract-run-attempt: \$\{\{ steps\.artifact\.outputs\.run_attempt \}\}/u,
    )
    assert.match(produce, /printf 'run_attempt=%s\\n' "\$GITHUB_RUN_ATTEMPT"/u)
    assert.match(
      produce,
      /name: native-contract-\$\{\{ github\.run_id \}\}\n[\s\S]*?overwrite: true/u,
    )
    assert.match(
      jobBlock(workflow, 'verify'),
      /contract-run-attempt: \$\{\{ needs\.produce\.outputs\.contract-run-attempt \}\}/u,
    )
  })

  it('checks downloaded test-product manifests against the uploader attempt', () => {
    for (const name of ['build-ios-app', 'build-ios-ipad']) {
      const job = jobBlock(workflow, name)
      assert.match(
        job,
        /EXPECTED_ARTIFACT_RUN_ATTEMPT: \$\{\{ needs\.build-ios-tests\.outputs\.artifact-attempt \}\}/u,
      )
      assert.match(job, /\.attempt' "\$manifest"\)" = "\$EXPECTED_ARTIFACT_RUN_ATTEMPT"/u)
      assert.doesNotMatch(job, /= "\$GITHUB_RUN_ATTEMPT"/u)
    }
    assert.match(
      jobBlock(workflow, 'test-swift-ui'),
      /EXPECTED_ARTIFACT_RUN_ATTEMPT: \$\{\{ needs\.build-swift-ui-tests\.outputs\.artifact-attempt \}\}/u,
    )
  })
})
