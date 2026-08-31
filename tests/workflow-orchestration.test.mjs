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

  it('repairs native Dependabot outputs through a verified artifact handoff', async () => {
    const workflow = await readWorkflow('repair-dependabot-native.yml')

    assert.match(workflow, /pull_request_target:/u)
    assert.match(workflow, /contents: read\n\s+pull-requests: read/u)
    assert.match(workflow, /persist-credentials: false/u)
    assert.match(workflow, /dependabot-repair\.mjs candidate/u)
    assert.match(workflow, /upload-artifact@/u)
    assert.match(workflow, /download-artifact@/u)
    assert.match(workflow, /dependabot-repair\.mjs provenance/u)
    assert.match(workflow, /dependabot-repair\.mjs artifact-paths/u)
    assert.match(workflow, /--force-with-lease=/u)
    assert.match(workflow, /restore-locks\.sh update/u)
    assert.match(workflow, /materialize-nuget/u)
    assert.doesNotMatch(workflow, /cp "\$candidate\/dotnet-clients\/Directory\.Packages\.props"/u)
    assert.match(workflow, /dependabot-android-repair/u)
    assert.match(workflow, /validateTrustedBaseDelta/u)
    assert.match(workflow, /if \[\[ "\$REPAIR_ANDROID" == true \]\]; then/u)
    assert.match(workflow, /contents\/Package\.swift\?ref=\$expected_revision/u)
    assert.doesNotMatch(workflow, /contents\/Package\.swift\?ref=\$skip_version/u)
    assert.match(workflow, /\.dependencyName == "source\.skip\.tools\/skip"/u)
    assert.doesNotMatch(workflow, /\.dependencyName == "skip"/u)
    assert.match(workflow, /echo 'repair_android=false'/u)
    assert.match(workflow, /GIT_CONFIG_KEY_0=http\.extraheader/u)
    assert.match(workflow, /runs-on: \[self-hosted, macOS, Tests\]/u)
    assert.match(workflow, /runs-on: \[self-hosted, Linux\]/u)
    assert.doesNotMatch(workflow, /contents: write/u)
    assert.match(workflow, /WRITE_TOKEN: \$\{\{ secrets\.DEPENDABOT_AUTOMERGE_TOKEN \}\}/u)
    assert.match(workflow, /Push with the single-command write credential/u)
  })

})
