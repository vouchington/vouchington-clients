import { mkdir, mkdtemp, readFile, rm, symlink, writeFile } from "node:fs/promises";
import assert from "node:assert/strict";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";

import { stageNativeContract } from "../scripts/stage-native-contract.mjs";

async function writeTree(root, files) {
  for (const [path, contents] of Object.entries(files)) {
    const file = join(root, path);
    await mkdir(dirname(file), { recursive: true });
    await writeFile(file, contents);
  }
}

async function fixture(t, { legacy = true } = {}) {
  const parent = await mkdtemp(join(tmpdir(), "voucha-contract-stage-"));
  const filamentsRoot = join(parent, "filaments");
  const consumerRoot = join(parent, "candidate-clients");
  const outputRoot = join(parent, "stage");
  await Promise.all([
    mkdir(consumerRoot, { recursive: true }),
    mkdir(outputRoot, { recursive: true }),
  ]);
  await writeTree(filamentsRoot, {
    "api-fixtures/v1/manifest.json": "{\"fixtures\":[]}\n",
    ...(legacy
      ? {
          "swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift":
            "enum UiMessageKey {}\n",
          "dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs":
            "class UiMessageKey {}\n",
        }
      : {}),
  });
  t.after(() => rm(parent, { recursive: true, force: true }));
  return { consumerRoot, filamentsRoot, outputRoot };
}

test("stages every declared legacy contract directory into an isolated root", async (t) => {
  const options = await fixture(t);
  assert.equal(await stageNativeContract(options), "legacy");
  assert.equal(
    await readFile(join(options.outputRoot, "api-fixtures/v1/manifest.json"), "utf8"),
    "{\"fixtures\":[]}\n",
  );
  assert.equal(
    await readFile(
      join(options.outputRoot, "swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift"),
      "utf8",
    ),
    "enum UiMessageKey {}\n",
  );
  assert.equal(
    await readFile(
      join(options.outputRoot, "dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs"),
      "utf8",
    ),
    "class UiMessageKey {}\n",
  );
});

test("uses the Filaments-owned exporter only when both legacy localization trees are absent", async (t) => {
  const options = await fixture(t, { legacy: false });
  const calls = [];
  await stageNativeContract({
    ...options,
    runExporter: async (arguments_) => {
      calls.push(arguments_);
      await writeTree(options.outputRoot, {
        "swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift":
          "exported swift\n",
        "dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs":
          "exported dotnet\n",
      });
    },
  });
  assert.deepEqual(calls, [
    {
      consumerRoot: options.consumerRoot,
      filamentsRoot: options.filamentsRoot,
      outputRoot: options.outputRoot,
    },
  ]);
  assert.equal(
    await readFile(join(options.outputRoot, "api-fixtures/v1/manifest.json"), "utf8"),
    "{\"fixtures\":[]}\n",
  );
});

test("fails closed for partial legacy trees, symlinks, a nonempty output, and invalid exporter output", async (t) => {
  const partial = await fixture(t, { legacy: false });
  await mkdir(
    join(partial.filamentsRoot, "swift-clients/ui/Sources/VouchaLocalization/Generated"),
    { recursive: true },
  );
  await assert.rejects(stageNativeContract(partial), /partial legacy localization directories/);

  const linked = await fixture(t);
  await symlink(
    "UiMessageKey.swift",
    join(
      linked.filamentsRoot,
      "swift-clients/ui/Sources/VouchaLocalization/Generated/alias.swift",
    ),
  );
  await assert.rejects(stageNativeContract(linked), /symbolic link/);

  const nonempty = await fixture(t);
  await mkdir(nonempty.outputRoot, { recursive: true });
  await writeFile(join(nonempty.outputRoot, "leftover"), "nope\n");
  await assert.rejects(stageNativeContract(nonempty), /stage output root must be empty/);

  const exporter = await fixture(t, { legacy: false });
  await assert.rejects(
    stageNativeContract({ ...exporter, runExporter: async () => {} }),
    /missing directory.*Generated/,
  );

  const failedExporter = await fixture(t, { legacy: false });
  await assert.rejects(
    stageNativeContract({
      ...failedExporter,
      runExporter: async () => {
        throw new Error("exit 1");
      },
    }),
    /Filaments exporter failed: exit 1/,
  );
});
