# Cursor configuration

Cursor is a supported local assistant for this repository. It reads the checked-in `AGENTS.md`
files and discovers shared skills from [`.agents/skills/`](../.agents/skills) (including the
tracked Claude discovery links in [`.claude/skills/`](../.claude/skills)). Do not add copied
`AGENTS.md` files, provider-specific skills, or Filaments web-agent configuration.

- No repository file registers an MCP server or grants an MCP tool. vouchington-machines registers
  the `vouchington-tooling` server and pre-approves its tools in user configuration.
- Keep command approvals limited to native-client project tools and repository checks. Generic
  shell defaults and sandbox paths belong in the host setup described by the [agent configuration
  ownership contract](https://github.com/vouchington/vouchington-machines/blob/main/docs/agent-config.md).
- Host credential-path guards are managed by `vouchington-machines` in user configuration. Run
  its `configure-agents.sh` before using this checkout; Cursor guards derive from the shared Claude policy.
- Sandbox profiles and machine-specific writable paths belong in the host setup described by the
  [agent configuration ownership contract](https://github.com/vouchington/vouchington-machines/blob/main/docs/agent-config.md).
- Cursor does not install repository hooks here. Run native checks from the documented Swift or
  .NET harnesses and use the [pr-shepherd plugin](../.claude/README.md#review-workflow) for PR
  iteration when requested.

`.cursor/worktrees/` is ignored runtime state. Cursor's worktree initializer is intentionally not
configured: setup remains explicit so it cannot assume a Filaments web or service environment.
