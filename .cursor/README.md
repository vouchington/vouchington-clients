# Cursor configuration

Cursor is a supported local assistant for this repository. It reads the checked-in `AGENTS.md`
files and discovers shared skills from [`.agents/skills/`](../.agents/skills) (including the
tracked Claude discovery links in [`.claude/skills/`](../.claude/skills)). Do not add copied
`AGENTS.md` files, provider-specific skills, or Filaments web-agent configuration.

- MCP uses the native [`.cursor/mcp.json`](mcp.json) registration with the pinned Agent Blackboard
  server and environment-only credentials.
- The exact eight-tool Agent Blackboard allowlist is repeated in [`cli.json`](cli.json) and
  [`permissions.json`](permissions.json). Keep it finite; never replace it with a wildcard.
- [`sandbox.json`](sandbox.json) is the portable `workspace_readwrite` profile. Native compilers
  and package managers may use the listed user caches, while credentials and unrelated home files
  remain outside the project policy.
- Cursor does not install repository hooks here. Run native checks from the documented Swift or
  .NET harnesses and use the [pr-shepherd plugin](../.claude/README.md#review-workflow) for PR
  iteration when requested.

`.cursor/worktrees/` is ignored runtime state. Cursor's worktree initializer is intentionally not
configured: setup remains explicit so it cannot assume a Filaments web or service environment.
