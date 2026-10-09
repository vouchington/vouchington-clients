# Grok configuration

Grok is a supported local assistant for this repository. It reads checked-in `AGENTS.md` files
and shared skills from [`.agents/skills/`](../.agents/skills); it does not receive copied
`AGENTS.md`, hooks, skills, or provider-specific permission files.

- No repository file registers an MCP server or grants an MCP tool. Grok uses the
  `vouchington-tooling` server that vouchington-machines registers per machine.
- Sandbox profiles, host-specific cache paths, and machine defaults belong in the host setup
  described by the [agent configuration ownership contract](https://github.com/vouchington/vouchington-machines/blob/main/docs/agent-config.md).
- Follow the Swift and .NET `AGENTS.md` files for native commands and boundaries. Use the
  [pr-shepherd plugin guidance](../.claude/README.md#review-workflow) when PR iteration is in
  scope.

`.grok/worktrees/` is ignored runtime state. No Filaments web, tmux, or session hooks are part of
this client repository.
