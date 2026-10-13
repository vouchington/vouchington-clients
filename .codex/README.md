# Codex configuration

Codex reads the checked-in `AGENTS.md` files directly. Do not add a CLAUDE.md fallback. Local skill
overlays live under [`.agents/skills/`](../.agents/skills).

No repository file installs a plugin or registers an MCP server. vouchington-machines'
`install-dependencies.sh` installs the `vouchington-workflow@vouchington`,
`vouchington-testing@vouchington`, and `pr-shepherd@jonathanong` plugins for Codex, and
`./configure-agents.sh` registers the `vouchington-tooling` MCP server in the user's Codex config
and pre-approves its tools. Run `./diagnose-agents.sh --repo <worktree>` there to check.

Verify the marketplace and plugin names before invoking an overlay:

```sh
codex plugin marketplace list
codex plugin list
```

If a required plugin is unavailable, stop and report it rather than applying the local overlay
alone. Use the local Swift or .NET test-authoring overlay only after its matching
`vouchington-testing` skill is available. Machine sandbox, model, approval-mode, and startup
defaults belong in the host setup described by the [agent configuration ownership
contract](https://github.com/vouchington/vouchington-machines/blob/main/docs/agent-config.md).
