# Grok configuration

Grok is a supported local assistant for this repository. It reads checked-in `CLAUDE.md` files
and shared skills from [`.agents/skills/`](../.agents/skills); it does not receive copied
`AGENTS.md`, hooks, skills, or provider-specific permission files.

- The native MCP registration and exact eight-tool allowlist live in [`config.toml`](config.toml).
  Credentials are read only from `AGENT_BLACKBOARD_URL` and `AGENT_BLACKBOARD_TOKEN`.
- Launch with `grok --sandbox workspace-write` (or set `GROK_SANDBOX=workspace-write`) to use the
  portable profile in [`sandbox.toml`](sandbox.toml).
- Follow the Swift and .NET `CLAUDE.md` files for native commands and boundaries. Use the
  [pr-shepherd plugin guidance](../.claude/README.md#review-workflow) when PR iteration is in
  scope.

`.grok/worktrees/` is ignored runtime state. No Filaments web, tmux, or session hooks are part of
this client repository.
