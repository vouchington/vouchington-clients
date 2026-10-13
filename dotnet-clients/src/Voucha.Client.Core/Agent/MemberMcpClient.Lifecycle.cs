using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpClient
{
  private const string SupportedProtocolVersion = "2025-06-18";

  private async Task EnsureInitializedAsync(string accessToken, CancellationToken cancellationToken)
  {
    lock (sessionGate)
    {
      ObjectDisposedException.ThrowIf(closed, this);
      if (initialized) return;
    }
    await initializeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      lock (sessionGate)
      {
        ObjectDisposedException.ThrowIf(closed, this);
        if (initialized) return;
        protocolVersion = null;
        sessionId = null;
        sessionAccessToken = null;
      }
      var result = await SendRpcAsync("initialize", new
      {
        protocolVersion = SupportedProtocolVersion,
        capabilities = new { },
        clientInfo = new { name = "voucha-native", version = "1.0.0" }
      }, accessToken, cancellationToken).ConfigureAwait(false);
      if (!result.TryGetProperty("protocolVersion", out var negotiated) ||
          negotiated.GetString() != SupportedProtocolVersion)
        throw new InvalidDataException("MCP server selected an unsupported protocol version.");
      lock (sessionGate) protocolVersion = SupportedProtocolVersion;
      await SendInitializedAsync(accessToken, cancellationToken).ConfigureAwait(false);
      lock (sessionGate)
      {
        initialized = true;
        sessionAccessToken = sessionId is null ? null : accessToken;
      }
    }
    catch
    {
      lock (sessionGate)
      {
        initialized = false;
        protocolVersion = null;
        sessionId = null;
        sessionAccessToken = null;
      }
      throw;
    }
    finally
    {
      initializeGate.Release();
    }
  }

  private async Task SendInitializedAsync(string accessToken, CancellationToken cancellationToken)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
    {
      Content = new StringContent(
          JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "notifications/initialized" }),
          Encoding.UTF8, "application/json")
    };
    request.Headers.Accept.ParseAdd("application/json, text/event-stream");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", SupportedProtocolVersion);
    if (sessionId is { } session) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", session);
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if (response.StatusCode == HttpStatusCode.Unauthorized) throw new McpUnauthorizedException();
    if (response.StatusCode != HttpStatusCode.Accepted)
      throw new HttpRequestException("MCP server rejected initialization notification.", null, response.StatusCode);
  }
}
