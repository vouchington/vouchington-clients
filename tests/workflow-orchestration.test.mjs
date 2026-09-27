import assert from 'node:assert/strict'
import { access, readFile } from 'node:fs/promises'
import { describe, it } from 'node:test'

const workflowUrl = name => new URL(`../.github/workflows/${name}`, import.meta.url)
const readWorkflow = name => readFile(workflowUrl(name), 'utf8')
const readPrompt = name =>
  readFile(new URL(`../docs/prompts/automation/${name}`, import.meta.url), 'utf8')

describe('event-driven CI orchestration', () => {
  it('uses the portable GitHub multiline output helper for automation requests', async () => {
    const invocations = [
      ['plan.yml', 'plan_request'],
      ['fix-issue.yml', 'fix_request'],
      ['shepherd.yml', 'pr_title'],
    ]

    for (const [workflowName, outputName] of invocations) {
      const workflow = await readWorkflow(workflowName)

      assert.match(
        workflow,
        new RegExp(
          `npx --yes pnpm@11\\.13\\.1 dlx vouchington-tooling@0\\.18\\.1 gha-output ${outputName}\\b`,
          'u',
        ),
      )
    }
  })

  it('authorizes organization members for slash-command automation', async () => {
    for (const [workflowName, promptName] of [
      ['plan.yml', 'plan.md'],
      ['fix-issue.yml', 'fix-issue.md'],
      ['shepherd.yml', 'shepherd.md'],
    ]) {
      const [workflow, prompt] = await Promise.all([
        readWorkflow(workflowName),
        readPrompt(promptName),
      ])
      assert.match(workflow, /fromJSON\('\["OWNER","COLLABORATOR","MEMBER"\]'\)/u)
      assert.match(
        workflow,
        /\.author_association == "OWNER" or \.author_association == "COLLABORATOR" or \.author_association == "MEMBER"/u,
      )
      assert.match(prompt, /`OWNER`, `COLLABORATOR`, or `MEMBER`/u)
    }
  })

  it('runs native contract tests on the pull request with one aggregate gate', async () => {
    await assert.rejects(access(workflowUrl('contract-parity.yml')))
    await assert.rejects(access(workflowUrl('native-contract-producer.yml')))
    await assert.rejects(access(workflowUrl('native-contract-result.yml')))

    const workflow = await readWorkflow('native-contract-tests.yml')

    assert.equal(
      workflow.includes('pull_request:\n    types: [opened, synchronize, reopened]\n'),
      true,
    )
    assert.equal(
      workflow.includes('ready_for_review') || workflow.includes('converted_to_draft'),
      false,
    )
    assert.doesNotMatch(workflow, /pull_request_target:/u)
    assert.match(workflow, /push:\n\s+branches: \[main\]/u)
    assert.match(
      workflow,
      /group: native-contract-tests-\$\{\{ github\.event\.pull_request\.number \|\| github\.ref \}\}/u,
    )
    assert.match(workflow, /cancel-in-progress: \$\{\{ github\.event_name == 'pull_request' \}\}/u)
    assert.match(workflow, / {2}tests:\n\s+name: Tests\n\s+if: always\(\)/u)
    assert.match(workflow, /jq -e 'all\(\.\[\]; \.result == "success"\)'/u)
    assert.match(
      workflow,
      /dotnet-portable:[\s\S]*?outputs:\n\s+dto-fixture-parity-outcome: \$\{\{ steps\.dotnet-dto-fixture-parity\.outcome \}\}/u,
    )
    assert.match(
      workflow,
      /test-swift-core:[\s\S]*?outputs:\n\s+dto-fixture-parity-outcome: \$\{\{ steps\.swift-dto-fixture-parity\.outcome \}\}/u,
    )
    assert.match(
      workflow,
      /DOTNET_OUTCOME: \$\{\{ needs\.dotnet-portable\.outputs\.dto-fixture-parity-outcome \}\}/u,
    )
    assert.match(
      workflow,
      /SWIFT_OUTCOME: \$\{\{ needs\.test-swift-core\.outputs\.dto-fixture-parity-outcome \}\}/u,
    )
    assert.doesNotMatch(workflow, /dto-fixture-parity-(?:dotnet|swift)-\$\{\{/u)
    assert.doesNotMatch(workflow, /workflow_run:|check-runs|Filaments contract parity/u)
    assert.doesNotMatch(workflow, /sleep 15|seq 1 240/u)
  })

  it('does not restart validate when a draft pull request is marked ready', async () => {
    const workflow = await readWorkflow('validate.yml')

    assert.equal(
      workflow.includes('pull_request:\n    types: [opened, synchronize, reopened]\n'),
      true,
    )
    assert.equal(
      workflow.includes('ready_for_review') || workflow.includes('converted_to_draft'),
      false,
    )
  })
})
