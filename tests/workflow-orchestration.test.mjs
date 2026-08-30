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

    assert.match(workflow, /pull_request_target:\n\s+types:/u)
    assert.match(workflow, /push:\n\s+branches: \[main\]/u)
    assert.match(
      workflow,
      /group: native-contract-tests-\$\{\{ github\.event\.pull_request\.number \|\| github\.ref \}\}/u,
    )
    assert.match(
      workflow,
      /cancel-in-progress: \$\{\{ github\.event_name == 'pull_request_target' \}\}/u,
    )
    assert.match(workflow, / {2}tests:\n\s+name: Tests\n\s+if: always\(\)/u)
    assert.match(workflow, /jq -e 'all\(\.\[\]; \.result == "success"\)'/u)
    assert.doesNotMatch(workflow, /workflow_run:|check-runs|Filaments contract parity/u)
    assert.doesNotMatch(workflow, /sleep 15|seq 1 240/u)
  })

  it('pins Final Code Review composites without a PAT router or labeled trigger', async () => {
    await assert.rejects(access(workflowUrl('validate-request-final-code-review.yml')))

    const workflow = await readWorkflow('final-code-review.yml')
    assert.match(
      workflow,
      /types: \[opened, reopened, synchronize, ready_for_review, converted_to_draft, closed\]/u,
    )
    assert.doesNotMatch(workflow, /final-code-review:requested|CODE_REVIEW_TRIGGER_TOKEN/u)
    assert.doesNotMatch(workflow, /sleep 15|seq 1 120/u)
    assert.match(workflow, /CI_WORKFLOW: validate\.yml/u)
    assert.match(workflow, /TESTS_JOB_NAME: validate/u)
    assert.match(
      workflow,
      /vouchington\/vouchington-tooling\/\.github\/actions\/final-review-select@f5f41caba5aef0b31e507a67123c76f1c9a53d02/u,
    )
    assert.match(
      workflow,
      /vouchington\/vouchington-tooling\/\.github\/actions\/final-review-gate@f5f41caba5aef0b31e507a67123c76f1c9a53d02/u,
    )
    assert.match(workflow, /CLAUDE_ENABLED: 'false'/u)
    assert.match(workflow, /'Code Reviewed' \|\| 'Ignore ineligible final review'/u)
  })
})
