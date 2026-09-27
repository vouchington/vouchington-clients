# Codex configuration

Codex reads the checked-in `AGENTS.md` files through the fallback configured in
[`config.toml`](config.toml), and discovers local overlays under [`.agents/skills/`](../.agents/skills).
Install the focused public plugins once in the Codex environment:

```sh
codex plugin marketplace add vouchington/vouchington-tooling
codex plugin marketplace add jonathanong/pr-shepherd
codex plugin add vouchington-workflow@vouchington
codex plugin add vouchington-testing@vouchington
codex plugin add pr-shepherd@jonathanong
codex plugin marketplace add jonathanong/agent-blackboard
codex plugin add agent-blackboard@agent-blackboard
```

Verify the marketplace and plugin names with `codex plugin marketplace list` and `codex plugin list`
after installation. If a required plugin is unavailable, stop and report it rather than applying
the local overlay alone. Use the local Swift or .NET test-authoring overlay only after its matching
`vouchington-testing` skill is available.

Set `AGENT_BLACKBOARD_URL` and `AGENT_BLACKBOARD_TOKEN` before starting Codex. The tracked
`.codex/config.toml` enables the upstream Agent Blackboard plugin and auto-approves only the eight
current tools. The project-scoped Claude and native Cursor/Grok registrations remain separately
pinned to `agent-blackboard@0.5.0`.
