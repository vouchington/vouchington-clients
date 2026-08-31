import assert from "node:assert/strict";
import { lstat, readFile } from "node:fs/promises";
import { describe, it } from "node:test";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));
const csharpRules = [
  "cs-core-no-platform-types",
  "cs-native-no-raw-markdown-authoring",
  "cs-navigation-catalog-no-hardcoded-labels",
  "cs-no-direct-presentation-formatting",
  "cs-no-hardcoded-display-copy",
  "cs-no-hardcoded-presentation-state",
  "cs-no-localhost-native-defaults",
  "cs-no-raw-local-llm-handler",
  "cs-omnisearch-no-hardcoded-copy",
];

describe("native quality ownership", () => {
  it("keeps the extracted native rule, skill, and agent surfaces discoverable", async () => {
    const config = await readFile(new URL("../sgconfig.yml", import.meta.url), "utf8");
    assert.match(config, /CSharp:/);
    assert.match(config, /Html:/);
    for (const id of [...csharpRules, "xaml-native-no-raw-markdown-label"]) {
      await lstat(new URL(`../ast-grep-rules/${id}.yml`, import.meta.url));
    }
    for (const skill of ["swift-test-authoring", "dotnet-test-authoring"]) {
      const skillPath = new URL(`../.agents/skills/${skill}/SKILL.md`, import.meta.url);
      assert.match(await readFile(skillPath, "utf8"), /vouchington-clients/);
      assert.ok((await lstat(new URL(`../.claude/skills/${skill}`, import.meta.url))).isSymbolicLink());
      const agent = await readFile(new URL(`../.codex/agents/${skill}.toml`, import.meta.url), "utf8");
      assert.match(agent, /gpt-5\.6-terra/);
    }
    const noMistakes = await readFile(new URL("../.no-mistakes.yml", import.meta.url), "utf8");
    assert.match(noMistakes, /csharp-max-lines-per-file/);
    assert.match(noMistakes, /csharp-no-async-void-delegate/);
  });
});
