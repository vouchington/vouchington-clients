import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { describe, it } from "node:test";

const readWorkflow = (name) =>
  readFile(new URL(`../.github/workflows/${name}`, import.meta.url), "utf8");

describe("native contract workflow boundary", () => {
  it("keeps contract path ownership in the checked-in configuration", async () => {
    const config = JSON.parse(
      await readFile(new URL("../contracts/filaments.json", import.meta.url), "utf8"),
    );
    const consumers = await Promise.all([
      readFile(new URL("../scripts/contract-artifact.mjs", import.meta.url), "utf8"),
      readWorkflow("native-contract-producer.yml"),
      readWorkflow("native-contract-tests.yml"),
      readWorkflow("contract-parity.yml"),
    ]);

    for (const path of config.paths) {
      for (const consumer of consumers) assert.equal(consumer.includes(path), false);
    }
  });

  it("uses full checkouts and pinned artifact actions", async () => {
    const workflows = await Promise.all([
      readWorkflow("native-contract-producer.yml"),
      readWorkflow("native-contract-tests.yml"),
      readWorkflow("contract-parity.yml"),
    ]);
    const combined = workflows.join("\n");

    assert.doesNotMatch(combined, /sparse-checkout/u);
    assert.match(combined, /actions\/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a/u);
    assert.match(combined, /actions\/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c/u);
  });

  it("keeps the Filaments secret in the trusted producer", async () => {
    const producer = await readWorkflow("native-contract-producer.yml");
    const consumer = await readWorkflow("native-contract-tests.yml");
    const gate = await readWorkflow("contract-parity.yml");

    assert.match(producer, /pull_request_target:/u);
    assert.match(producer, /secrets\.FILAMENTS_DEPLOY_KEY/u);
    assert.doesNotMatch(consumer, /secrets\./u);
    assert.doesNotMatch(gate, /secrets\./u);
    assert.match(consumer, /workflow_run:/u);
  });

  it("preserves exact run identities and the required check name", async () => {
    const producer = await readWorkflow("native-contract-producer.yml");
    const consumer = await readWorkflow("native-contract-tests.yml");
    const gate = await readWorkflow("contract-parity.yml");

    assert.match(producer, /Native contract producer for PR/u);
    assert.match(producer, /pull_request\.updated_at/u);
    assert.match(gate, /EXPECTED_UPDATED_AT/u);
    assert.match(producer, /retention-days: 1/u);
    assert.match(consumer, /run-id: \$\{\{ github\.event\.workflow_run\.id \}\}/u);
    assert.match(consumer, /--expected-producer-run-attempt/u);
    assert.match(gate, /name: Filaments contract parity/u);
    assert.match(consumer, /Native contract tests for producer.*run_attempt/u);
    assert.match(gate, /Native contract tests for producer.*producer_attempt/u);
  });
});
