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
    assert.doesNotMatch(workflow, /sleep 15|seq 1 240/u)
  })

  it('routes the exact completed native test run into Final Code Review', async () => {
    const workflow = await readWorkflow('final-code-review.yml')
    const request = await readWorkflow('request-final-review.yml')
    assert.match(request, /workflow_run:\n\s+workflows: \[Native contract tests\]/u)
    assert.match(request, /source-run-id: \$\{\{ github\.event\.workflow_run\.id \}\}/u)
    assert.match(request, /fan-in-job: tests/u)
    assert.match(request, /dispatch-event-type: final-review-requested/u)
    assert.match(workflow, /repository_dispatch:\n\s+types: \[final-review-requested\]/u)
    assert.doesNotMatch(workflow, /pull_request(?:_target)?:/u)
    assert.match(
      workflow,
      /source-run-id: \$\{\{ github\.event\.client_payload\.source_run_id \}\}/u,
    )
    assert.doesNotMatch(workflow, /TESTS_WAIT_|sleep 30|seq 1 160/u)
    for (const action of ['select-final-review', 'final-review-gate'])
      assert.match(
        workflow,
        new RegExp(
          `vouchington/vouchington-tooling/\\.github/actions/${action}@[a-f0-9]{40} # v\\d+\\.\\d+\\.\\d+`,
          'u',
        ),
      )
    assert.match(
      request,
      /vouchington\/vouchington-tooling\/\.github\/actions\/request-final-review@[a-f0-9]{40} # v\d+\.\d+\.\d+/u,
    )
    assert.match(workflow, /CLAUDE_ENABLED: 'false'/u)
    assert.match(workflow, /name: Code Reviewed/u)
    assert.match(workflow, /check_name: Code Reviewed/u)
  })
})
