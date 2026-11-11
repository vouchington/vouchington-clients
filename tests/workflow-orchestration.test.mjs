import assert from 'node:assert/strict'
import { access, readFile } from 'node:fs/promises'
import { describe, it } from 'node:test'

const workflowUrl = name => new URL(`../.github/workflows/${name}`, import.meta.url)
const readWorkflow = name => readFile(workflowUrl(name), 'utf8')

describe('event-driven CI orchestration', () => {
  it('classifies Swift Android repairs through the trusted classifier script', async () => {
    const [workflow, classifier] = await Promise.all([
      readWorkflow('dependabot-automerge.yml'),
      readFile(
        new URL('../scripts/classify-dependabot-swift-android-repair.mjs', import.meta.url),
        'utf8',
      ),
    ])

    assert.match(
      workflow,
      /- name: Classify trusted Swift Android repair[\s\S]*?node scripts\/classify-dependabot-swift-android-repair\.mjs/u,
    )
    assert.match(
      workflow,
      /if: steps\.live\.outputs\.eligible == 'true' && steps\.classify\.outputs\.repair-kind == 'swift-android'/u,
    )
    assert.match(classifier, /validate-dependabot-swift-repair\.mjs/u)
    assert.match(classifier, /repairKind = 'swift-android'/u)
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
    assert.doesNotMatch(workflow, /ready_for_review|converted_to_draft/u)
    assert.doesNotMatch(workflow, /pull_request_target:/u)
    assert.match(workflow, /push:\n\s+branches: \[main\]/u)
    assert.match(
      workflow,
      /group: native-contract-tests-\$\{\{ github\.event\.pull_request\.number \|\| github\.ref \}\}/u,
    )
    assert.match(workflow, /cancel-in-progress: \$\{\{ github\.event_name == 'pull_request' \}\}/u)
    assert.match(workflow, / {2}tests:\n\s+name: Tests\n\s+if: \$\{\{ !cancelled\(\) \}\}/u)
    assert.match(
      workflow,
      /all\(to_entries\[\] \| select\(\.key != "dotnet-windows-image-preview" and \.key != "dotnet-windows-language-model-surface" and \.key != "dotnet-windows-maui"\); \.value\.result == "success"\)/u,
    )
    assert.ok(workflow.includes('((."dotnet-windows-image-preview".result == "success") or'))
    assert.ok(
      workflow.includes(
        '($route == "false" and ."dotnet-windows-image-preview".result == "skipped"))',
      ),
    )
    assert.match(
      workflow,
      /WINDOWS_IMAGE_PREVIEW_ROUTE: \$\{\{ needs\.verify\.outputs\.windows-image-preview \}\}/u,
    )
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
