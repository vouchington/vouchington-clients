import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, it } from "node:test";

const root = resolve(import.meta.dirname, "..");
const tools = [
  "entry_append",
  "entry_get",
  "session_archive",
  "session_create",
  "session_ensure",
  "session_patch",
  "session_search",
  "snapshot_export",
].map((name) => `mcp__agent-blackboard__${name}`);

function readJson(path) {
  return JSON.parse(readFileSync(resolve(root, path), "utf8"));
}

describe("Agent Blackboard host configuration", () => {
  it("keeps Claude MCP registration pinned and environment-only", () => {
    assert.deepEqual(readJson(".mcp.json"), {
      mcpServers: {
        "agent-blackboard": {
          command: "npx",
          args: ["-y", "agent-blackboard@0.5.0", "mcp"],
          env: {
            AGENT_BLACKBOARD_URL: "${AGENT_BLACKBOARD_URL}",
            AGENT_BLACKBOARD_TOKEN: "${AGENT_BLACKBOARD_TOKEN}",
          },
        },
      },
    });
  });

  it("pre-authorizes exactly the current eight MCP tools", () => {
    const settings = readJson(".claude/settings.json");
    assert.deepEqual(settings.enabledMcpjsonServers, ["agent-blackboard"]);
    assert.deepEqual(settings.permissions?.allow, tools);
    assert.equal(
      settings.permissions.allow.some((tool) => tool.includes("*")),
      false,
      "MCP approval must not use a wildcard",
    );
  });

  it("documents the upstream Codex plugin registration", () => {
    const instructions = readFileSync(resolve(root, ".codex/README.md"), "utf8");
    assert.match(instructions, /codex plugin marketplace add jonathanong\/agent-blackboard/u);
    assert.match(instructions, /codex plugin add agent-blackboard@agent-blackboard/u);
    assert.match(instructions, /agent-blackboard@0\.5\.0/u);
    const config = readFileSync(resolve(root, ".codex/config.toml"), "utf8");
    assert.match(config, /\[plugins\."agent-blackboard@agent-blackboard"\]\nenabled = true/u);
    const approvals = [...config.matchAll(/\.tools\.([a-z_]+)\]\napproval_mode = "approve"/gu)].map(
      (match) => match[1],
    );
    assert.equal((config.match(/approval_mode = "approve"/gu) ?? []).length, 8);
    assert.deepEqual(approvals, [
      "entry_append",
      "entry_get",
      "session_archive",
      "session_create",
      "session_ensure",
      "session_patch",
      "session_search",
      "snapshot_export",
    ]);
  });

  it("requires fail-closed, explicit session journaling in root instructions", () => {
    const instructions = readFileSync(resolve(root, "CLAUDE.md"), "utf8");
    assert.match(instructions, /upstream `agent-blackboard` plugin/u);
    assert.match(instructions, /`vouchington-workflow:blackboard`/u);
    assert.match(instructions, /Session ids.*must\s+be\s+explicit/isu);
    assert.match(instructions, /fail closed/iu);
  });
});
