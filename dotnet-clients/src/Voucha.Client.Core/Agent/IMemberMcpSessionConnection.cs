using System.Text.Json;

namespace Voucha.Client.Core.Agent;

public interface IMemberMcpSystemBrowser
{
  Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken);
}

public interface IMemberMcpSessionConnectionFactory
{
  Task<IMemberMcpSessionConnection> CreateAsync(string accountId, CancellationToken cancellationToken);

  /// <summary>
  /// Factory whose connections can revoke grants stored by the retired browser authorization.
  /// Null when connections created here already do so.
  /// </summary>
  IMemberMcpSessionConnectionFactory? LegacyGrantRevocationFactory => null;
}

public interface IMemberMcpSessionConnection : IAsyncDisposable
{
  Task<JsonElement> ConnectAndListToolsAsync(CancellationToken cancellationToken);
  Task<JsonElement> AuthorizeAndListToolsAsync(string scope, CancellationToken cancellationToken) =>
      throw new NotSupportedException("Explicit MCP consent is unavailable.");
  Task<JsonElement> ListToolsAsync(CancellationToken cancellationToken);
  Task<McpToolResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken);
  Task DisconnectAsync(CancellationToken cancellationToken);
}
