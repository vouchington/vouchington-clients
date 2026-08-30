import assert from 'node:assert/strict'
import { access, readFile } from 'node:fs/promises'
import { describe, it } from 'node:test'

const workflowUrl = name => new URL(`../.github/workflows/${name}`, import.meta.url)
const readWorkflow = name => readFile(workflowUrl(name), 'utf8')

describe('event-driven CI orchestration', () => {
  it('runs native contract tests on the pull request with one aggregate gate', async () => {
    await assert.rejects(access(workflowUrl('contract-parity.yml')))
    await assert.rejects(access(workflowUrl('native-contract-producer.yml')))
    await assert.rejects(access(workflowUrl('native-contract-result.yml')))

    const workflow = await readWorkflow('native-contract-tests.yml')

    assert.match(workflow, /pull_request:\n\s+types:/u)
    assert.doesNotMatch(workflow, /pull_request_target:/u)
    assert.match(workflow, /push:\n\s+branches: \[main\]/u)
    assert.match(
      workflow,
      /group: native-contract-tests-\$\{\{ github\.event\.pull_request\.number \|\| github\.ref \}\}/u,
    )
    assert.match(
      workflow,
      /cancel-in-progress: \$\{\{ github\.event_name == 'pull_request' \}\}/u,
    )
    assert.match(workflow, / {2}tests:\n\s+name: Tests\n\s+if: always\(\)/u)
    assert.match(workflow, /jq -e 'all\(\.\[\]; \.result == "success"\)'/u)
    assert.doesNotMatch(workflow, /workflow_run:|check-runs|Filaments contract parity/u)
    assert.doesNotMatch(workflow, /sleep [0-9]|seq 1 240/u)
  })

  it('routes Final Code Review from exact validation completion without polling', async () => {
    await assert.rejects(access(workflowUrl('validate-request-final-code-review.yml')))

    const workflow = await readWorkflow('final-code-review.yml')
    const request = await readWorkflow('request-final-review.yml')
    const stop = await readWorkflow('stop-final-review.yml')
    assert.match(workflow, /repository_dispatch:\n\s+types: \[final-review-requested\]/u)
    assert.match(request, /workflow_run:\n\s+workflows: \[Validate\]/u)
    assert.match(request, /source-run-attempt: \$\{\{ github\.event\.workflow_run\.run_attempt \}\}/u)
    assert.match(workflow, /workflow-path: \.github\/workflows\/validate\.yml/u)
    assert.match(workflow, /fan-in-job: validate/u)
    assert.match(stop, /types: \[converted_to_draft, closed\]/u)
    assert.match(stop, /cancel-in-progress: true/u)
    const pins = [...`${workflow}\n${request}`.matchAll(/vouchington-tooling\/\.github\/actions\/[^@]+@([0-9a-f]{40})/gu)].map(match => match[1])
    assert.ok(pins.length >= 7)
    assert.equal(new Set(pins).size, 1)
    assert.doesNotMatch(`${workflow}\n${request}\n${stop}`, /TESTS_WAIT|WAIT_(?:ATTEMPTS|SECONDS)|sleep [0-9]/u)
    assert.match(workflow, /CLAUDE_ENABLED: 'false'/u)
    assert.match(workflow, /name: Code Reviewed/u)
    assert.match(workflow, /checks: write/u)
  })
})
