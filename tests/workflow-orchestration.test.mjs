import assert from 'node:assert/strict'
import { access, readFile } from 'node:fs/promises'
import { describe, it } from 'node:test'

const workflowUrl = name => new URL(`../.github/workflows/${name}`, import.meta.url)
const readWorkflow = name => readFile(workflowUrl(name), 'utf8')
const readPrompt = name =>
  readFile(new URL(`../docs/prompts/automation/${name}`, import.meta.url), 'utf8')

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
})

describe('automation command authorization', () => {
  for (const workflowName of ['plan.yml', 'fix-issue.yml', 'shepherd.yml']) {
    it(`authorizes organization members consistently in ${workflowName}`, async () => {
      const workflow = await readWorkflow(workflowName)

      assert.match(workflow, /fromJSON\('\["OWNER","COLLABORATOR","MEMBER"\]'\)/u)
      assert.match(
        workflow,
        /\.author_association == "OWNER" or \.author_association == "COLLABORATOR" or \.author_association == "MEMBER"/u,
      )
      assert.doesNotMatch(workflow, /"CONTRIBUTOR"|"NONE"/u)
    })
  }

  it('requires the same live authorization before agent mutations', async () => {
    for (const promptName of ['plan.md', 'fix-issue.md', 'shepherd.md']) {
      const prompt = await readPrompt(promptName)
      assert.match(
        prompt,
        /live\s+`author_association` to be exactly `OWNER`, `COLLABORATOR`, or `MEMBER`/u,
      )
    }

    assert.match(
      await readPrompt('shepherd.md'),
      /require its body to remain exactly `\/shepherd`/u,
    )
  })
})
