import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";
import { describe, it } from "node:test";

const workflowUrl = (name) => new URL(`../.github/workflows/${name}`, import.meta.url);
const readWorkflow = (name) => readFile(workflowUrl(name), "utf8");

describe("event-driven CI orchestration", () => {
  it("reports native contract completion without a polling gate", async () => {
    await assert.rejects(access(workflowUrl("contract-parity.yml")));

    const [producer, consumer, result] = await Promise.all([
      readWorkflow("native-contract-producer.yml"),
      readWorkflow("native-contract-tests.yml"),
      readWorkflow("native-contract-result.yml"),
    ]);

    assert.match(producer, /checks: write/u);
    assert.match(producer, /name: Create required contract parity check/u);
    assert.match(producer, /external_id=.*native-contract-producer/u);
    assert.match(consumer, /Native contract tests for producer.*PR #.*head/u);
    assert.match(
      consumer,
      /group: native-contract-tests-\$\{\{ github\.event\.workflow_run\.pull_requests\[0\]\.number \|\| github\.event\.workflow_run\.id \}\}/u,
    );
    assert.match(result, /workflows: \[Native contract tests\]/u);
    assert.match(result, /types: \[completed\]/u);
    assert.match(result, /external_id/u);
    assert.match(result, /check-runs/u);
    assert.match(result, /conclusion="success"/u);
    assert.doesNotMatch(`${producer}\n${consumer}\n${result}`, /sleep 15|seq 1 240/u);
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
