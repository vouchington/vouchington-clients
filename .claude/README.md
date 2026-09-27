# Claude Code configuration

Claude Code uses the project-scoped [`../.mcp.json`](../.mcp.json) registration, enabled by
`enabledMcpjsonServers`. It runs the published Agent Blackboard MCP server at the pinned
`agent-blackboard@0.5.0` version. Export `AGENT_BLACKBOARD_URL` and `AGENT_BLACKBOARD_TOKEN` before
starting Claude Code.

## Workflow plugins

The checked-in settings enable the portable workflow and testing plugins plus pr-shepherd. Install
them after registering their public marketplaces, then restart Claude Code:

```sh
claude plugin marketplace add vouchington/vouchington-tooling --scope project --sparse .claude-plugin plugins
claude plugin marketplace add jonathanong/pr-shepherd --scope project
claude plugin install vouchington-workflow@vouchington --scope project
claude plugin install vouchington-testing@vouchington --scope project
claude plugin install pr-shepherd@jonathanong --scope project
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

The project settings pre-authorize only the eight current Agent Blackboard tools. Session ids,
agent names, and parent-session ids remain explicit inputs; the server must not infer them.
