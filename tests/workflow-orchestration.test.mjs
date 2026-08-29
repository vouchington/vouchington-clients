import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";
import { describe, it } from "node:test";

const workflowUrl = (name) => new URL(`../.github/workflows/${name}`, import.meta.url);
const readWorkflow = (name) => readFile(workflowUrl(name), "utf8");

describe("event-driven CI orchestration", () => {
  it("runs native contract tests on the pull request with one aggregate gate", async () => {
    await assert.rejects(access(workflowUrl("contract-parity.yml")));
    await assert.rejects(access(workflowUrl("native-contract-producer.yml")));
    await assert.rejects(access(workflowUrl("native-contract-result.yml")));

    const workflow = await readWorkflow("native-contract-tests.yml");

    assert.match(workflow, /pull_request_target:\n\s+types:/u);
    assert.match(workflow, /push:\n\s+branches: \[main\]/u);
    assert.match(
      workflow,
      /group: native-contract-tests-\$\{\{ github\.event\.pull_request\.number \|\| github\.ref \}\}/u,
    );
    assert.match(
      workflow,
      /cancel-in-progress: \$\{\{ github\.event_name == 'pull_request_target' \}\}/u,
    );
    assert.match(workflow, /  tests:\n\s+name: Tests\n\s+if: always\(\)/u);
    assert.match(workflow, /jq -e 'all\(\.\[\]; \.result == "success"\)'/u);
    assert.doesNotMatch(workflow, /workflow_run:|check-runs|Filaments contract parity/u);
    assert.doesNotMatch(workflow, /sleep 15|seq 1 240/u);
  });

  it("starts final review from the validated label without waiting", async () => {
    const workflow = await readWorkflow("final-code-review.yml");

    assert.match(workflow, /pull_request_target:\n\s+types: \[labeled\]/u);
    assert.doesNotMatch(workflow, /ready_for_review|validation_state|sleep 15|seq 1 120/u);
    assert.match(workflow, /github\.event\.label\.name == 'final-code-review:requested'/u);
    assert.match(workflow, /Select the exact validated PR head/u);
    assert.match(workflow, /\.path == "\.github\/workflows\/validate\.yml"/u);
    assert.match(workflow, /\[ "\$validate_result" = success \]/u);
  });
});
