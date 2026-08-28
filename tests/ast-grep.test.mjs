import assert from "node:assert/strict";
import { access } from "node:fs/promises";
import { spawnSync } from "node:child_process";
import { describe, it } from "node:test";
import { fileURLToPath } from "node:url";

const repoRoot = fileURLToPath(new URL("..", import.meta.url));
const astGrep = fileURLToPath(new URL("../node_modules/@ast-grep/cli/ast-grep", import.meta.url));

function runAstGrep(...args) {
  const result = spawnSync(astGrep, args, { cwd: repoRoot, encoding: "utf8" });
  assert.equal(result.error, undefined, result.error?.message);
  assert.equal(result.status, 0, `${result.stdout}\n${result.stderr}`);
}

describe("Swift ast-grep guards", () => {
  it("validates every guard's positive and negative examples", async () => {
    await access(astGrep);
    runAstGrep("test");
  });

  it("scans the client-owned Swift sources with error-level enforcement", async () => {
    await access(astGrep);
    runAstGrep("scan", "--error", "--no-ignore", "hidden", "--", "swift-clients/");
  });
});
