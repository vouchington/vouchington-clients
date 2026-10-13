using System.Net;
using System.Net.Http.Headers;

namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpClient
{
  /// <summary>Terminate a stateful MCP session before releasing this transport.</summary>
  public async Task CloseAsync(string accessToken, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
    await initializeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try { await CloseSessionAsync(accessToken, cancellationToken).ConfigureAwait(false); }
    finally { initializeGate.Release(); }
  }

  private async Task CloseSessionAsync(string? accessToken, CancellationToken cancellationToken)
  {
    string? closingSession;
    string? version;
    lock (sessionGate)
    {
      closed = true;
      closingSession = sessionId;
      version = protocolVersion;
      initialized = false;
      sessionId = null;
      protocolVersion = null;
      sessionAccessToken = null;
    }
    if (closingSession is null) return;
    if (accessToken is null) throw new InvalidOperationException("MCP session has no access token.");
    using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    request.Headers.TryAddWithoutValidation("Mcp-Session-Id", closingSession);
    if (version is not null) request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", version);
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if (response.StatusCode == HttpStatusCode.Unauthorized) throw new McpUnauthorizedException();
    if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Accepted or
        HttpStatusCode.NoContent or HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed))
      throw new HttpRequestException("MCP session termination failed.", null, response.StatusCode);
  }

  public async ValueTask DisposeAsync()
  {
    try
    {
      await initializeGate.WaitAsync().ConfigureAwait(false);
      try
      {
        string? accessToken;
        lock (sessionGate) accessToken = sessionAccessToken;
        await CloseSessionAsync(accessToken, CancellationToken.None).ConfigureAwait(false);
      }
      finally { initializeGate.Release(); }
    }
    finally
    {
      Dispose();
    }
  }
}
