using System.Text.Json;

namespace Voucha.Client.Core.Agent;

public interface IMemberMcpSystemBrowser
{
  Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken);
}

public interface IMemberMcpSessionConnectionFactory
{
  Task<IMemberMcpSessionConnection> CreateAsync(string accountId, CancellationToken cancellationToken);
}

public interface IMemberMcpSessionConnection : IAsyncDisposable
{
  Task<JsonElement> ConnectAndListToolsAsync(CancellationToken cancellationToken);
  Task<JsonElement> ListToolsAsync(CancellationToken cancellationToken);
  Task<McpToolResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken);
  Task DisconnectAsync(CancellationToken cancellationToken);
}
