import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { describe, it } from "node:test";

const readWorkflow = (name) =>
  readFile(new URL(`../.github/workflows/${name}`, import.meta.url), "utf8");
const readAction = (name) =>
  readFile(new URL(`../.github/actions/${name}/action.yml`, import.meta.url), "utf8");

const preparedCandidateInputs = [
  "candidate-merge-sha: ${{ needs.verify.outputs.merge-sha }}",
  "producer-run-attempt: ${{ github.event.workflow_run.run_attempt }}",
  "producer-run-id: ${{ github.event.workflow_run.id }}",
];

const cleanupWorkspace = (workflow) => {
  assert.match(workflow, /Clean persistent runner workspace/u);
  assert.match(workflow, /PRESERVE_NODE_MODULES: 'false'/u);
};

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
    const files = await Promise.all([
      readWorkflow("native-contract-producer.yml"),
      readWorkflow("native-contract-tests.yml"),
      readWorkflow("contract-parity.yml"),
      readAction("prepare-native-contract"),
    ]);
    const combined = files.join("\n");

    assert.doesNotMatch(combined, /sparse-checkout/u);
    assert.match(combined, /actions\/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a/u);
    assert.match(combined, /actions\/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c/u);
  });

  it("prepares the exact verified candidate for every native test job", async () => {
    const [action, workflow] = await Promise.all([
      readAction("prepare-native-contract"),
      readWorkflow("native-contract-tests.yml"),
    ]);

    for (const input of ["candidate-merge-sha", "producer-run-id", "producer-run-attempt"])
      assert.match(action, new RegExp(`inputs\\.${input}`, "u"));
    for (const expectation of [
      /fetch-depth: 0/u,
      /ref: \$\{\{ inputs\.candidate-merge-sha \}\}/u,
      /path: candidate-clients/u,
      /persist-credentials: false/u,
      /run-id: \$\{\{ inputs\.producer-run-id \}\}/u,
      /native-contract-\$\{\{ inputs\.producer-run-id \}\}-\$\{\{ inputs\.producer-run-attempt \}\}/u,
      /--expected-merge-sha "\$\{\{ inputs\.candidate-merge-sha \}\}"/u,
    ])
      assert.match(action, expectation);

    for (const input of preparedCandidateInputs) assert.equal(workflow.split(input).length - 1, 2);
  });

  it("runs native .NET tests on the supported runner matrix with coverage and cleanup", async () => {
    const [action, workflow] = await Promise.all([
      readAction("prepare-native-contract"),
      readWorkflow("native-contract-tests.yml"),
    ]);

    for (const runner of [
      "runner: [self-hosted, Linux, Docker, Tests]",
      "runner: [self-hosted, macOS, Tests]",
    ])
      assert.ok(workflow.includes(runner));
    assert.match(workflow, /dotnet test Voucha\.DotNet\.sln[\s\S]*XPlat Code Coverage/u);
    assert.match(workflow, /coverage\.info[\s\S]*TestResults\/core\/lcov\.info/u);
    assert.match(workflow, /pnpm run coverage:dotnet-core/u);
    for (const command of [
      /restore-locks\.sh verify/u,
      /dotnet build dotnet-clients\/tests\/Voucha\.Client\.App\.Tests\/Voucha\.Client\.App\.Tests\.csproj/u,
      /dotnet test dotnet-clients\/tests\/Voucha\.Client\.App\.Tests\/Voucha\.Client\.App\.Tests\.csproj/u,
      /dotnet build dotnet-clients\/src\/Voucha\.Client\.App\/Voucha\.Client\.App\.csproj[\s\S]*--framework net10\.0-maccatalyst/u,
    ])
      assert.match(workflow, command);
    cleanupWorkspace(action);
    cleanupWorkspace(workflow);
  });

  it("keeps the Filaments secret in the trusted producer", async () => {
    const producer = await readWorkflow("native-contract-producer.yml");
    const consumer = await readWorkflow("native-contract-tests.yml");
    const gate = await readWorkflow("contract-parity.yml");
    const action = await readAction("prepare-native-contract");

    assert.match(producer, /pull_request_target:/u);
    assert.match(producer, /secrets\.FILAMENTS_DEPLOY_KEY/u);
    assert.doesNotMatch(consumer, /secrets\./u);
    assert.doesNotMatch(gate, /secrets\./u);
    assert.doesNotMatch(action, /secrets\./u);
    assert.match(consumer, /workflow_run:/u);
  });

  it("preserves exact run identities and the required check name", async () => {
    const producer = await readWorkflow("native-contract-producer.yml");
    const consumer = await readWorkflow("native-contract-tests.yml");
    const gate = await readWorkflow("contract-parity.yml");

    assert.match(
      producer,
      /run-name: >-\n\s+Native contract producer for PR #\$\{\{ github\.event\.pull_request\.number \}\} at \$\{\{ github\.event\.pull_request\.head\.sha \}\} updated \$\{\{ github\.event\.pull_request\.updated_at \}\}/u,
    );
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
