# Claude Code configuration

No repository file registers an MCP server, plugin, marketplace, or tool approval.
vouchington-machines registers the `vouchington-tooling` MCP server once per machine and
pre-approves its tools; journal through it with the `vouchington-workflow:blackboard` skill.

Use Claude Code v2.1.281 or later for this repository's `AGENTS.md` instructions. Direct
`AGENTS.md` loading began in v2.1.277, but earlier versions could miss it in some sessions,
including Amazon Bedrock or sessions with telemetry disabled. Check `claude --version` and confirm
the repository's `AGENTS.md` appears in `/context` before working. See the
[Claude Code instruction-file guide](https://code.claude.com/docs/en/memory#when-agents-md-support-is-unavailable).

If you keep a local `CLAUDE.local.md`, Claude Code's default instruction setting loads that file
instead of `AGENTS.md`. Remove the local file or set Project instructions to
`claude-md-and-agents-md` in `/config`, then confirm that `/context` includes the root and scoped
native-client `AGENTS.md` files as you work in those directories.

## Workflow plugins

vouchington-machines enables the portable `vouchington-workflow@vouchington` and
`vouchington-testing@vouchington` plugins plus `pr-shepherd@jonathanong`, and registers their
marketplaces in each machine's user settings (`./configure-agents.sh`, then
`./diagnose-agents.sh --repo <worktree>`). If a plugin is missing, rerun the machine configuration
or install it by hand into user scope, never project scope, which would write a declaration into
this repository:

```sh
claude plugin marketplace add vouchington/vouchington-tooling --scope user --sparse .claude-plugin plugins
claude plugin marketplace add jonathanong/pr-shepherd --scope user
claude plugin install vouchington-workflow@vouchington --scope user
claude plugin install vouchington-testing@vouchington --scope user
claude plugin install pr-shepherd@jonathanong --scope user
```

Confirm with `claude plugin marketplace list`, `claude plugin list`, and
`claude plugin details <plugin>@<marketplace>`. The local Swift and .NET test-authoring files are
thin overlays: they must load the matching `vouchington-testing` skill first and then apply the
client's `AGENTS.md` and README guidance. If the canonical plugin is unavailable, stop and report
the prerequisite instead of using the overlay alone.

### Review workflow

Use `pr-shepherd@jonathanong` for PR creation or iteration only when the user requests that review
workflow. Its repository guidance is portable; it does not install Filaments web, tmux, session,
or post-edit hooks in this client checkout.

Session ids, agent names, and parent-session ids remain explicit journal inputs; the server must
not infer them. Plugins, marketplaces, MCP servers, tool approvals, and machine sandbox, model,
permission-mode, and startup defaults belong in the host setup described by the [agent
configuration ownership contract](https://github.com/vouchington/vouchington-machines/blob/main/docs/agent-config.md).
