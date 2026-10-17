using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpClient
{
  private async Task<bool> ReplyToServerRequestAsync(
      string payload, string accessToken, string? requestSession,
      string? version, CancellationToken cancellationToken)
  {
    using var document = JsonDocument.Parse(payload);
    var root = document.RootElement;
    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("method", out var method)) return false;
    if (method.ValueKind != JsonValueKind.String || !root.TryGetProperty("jsonrpc", out var jsonrpc) ||
        jsonrpc.GetString() != "2.0")
      throw new InvalidDataException("MCP server request is malformed.");
    if (!root.TryGetProperty("id", out var requestId)) return true;
    if (requestId.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
      throw new InvalidDataException("MCP server request has an invalid ID.");
    var body = method.GetString() == "ping"
        ? JsonSerializer.Serialize(new { jsonrpc = "2.0", id = requestId, result = new { } })
        : JsonSerializer.Serialize(new
        {
          jsonrpc = "2.0",
          id = requestId,
          error = new { code = -32601, message = "Method not found" }
        });
    cancellationToken.ThrowIfCancellationRequested();
    retryCooldown.Check();
    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json")
    };
    request.Headers.Accept.ParseAdd("application/json, text/event-stream");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    if (version is not null) request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", version);
    if (requestSession is not null) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", requestSession);
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if (response.StatusCode == HttpStatusCode.Unauthorized) throw new McpUnauthorizedException();
    if (response.StatusCode == HttpStatusCode.NotFound && requestSession is not null)
    {
      lock (sessionGate)
      {
        if (sessionId == requestSession)
        {
          initialized = false;
          protocolVersion = null;
          sessionId = null;
          sessionAccessToken = null;
        }
      }
      throw new McpSessionExpiredException();
    }
    if (response.StatusCode == HttpStatusCode.TooManyRequests)
      throw retryCooldown.FromResponse(response);
    if (response.StatusCode != HttpStatusCode.Accepted)
      throw new HttpRequestException("MCP server rejected a JSON-RPC response.", null, response.StatusCode);
    return true;
  }
}
