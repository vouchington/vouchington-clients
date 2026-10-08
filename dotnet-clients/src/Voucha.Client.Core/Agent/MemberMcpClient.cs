using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Voucha.Client.Core.Agent;

/// <summary>A bearer-only member MCP transport with no application session cookies.</summary>
public sealed class MemberMcpClient : IDisposable
{
  private readonly HttpClient client;
  private readonly Uri endpoint;
  private long nextRequestId;

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "HttpClient owns and disposes the handler.")]
  public MemberMcpClient(Uri siteOrigin, HttpMessageHandler? handler = null)
  {
    ArgumentNullException.ThrowIfNull(siteOrigin);
    if (siteOrigin.Scheme != Uri.UriSchemeHttps || siteOrigin.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(siteOrigin.UserInfo) ||
        !string.IsNullOrEmpty(siteOrigin.Query) || !string.IsNullOrEmpty(siteOrigin.Fragment))
      throw new ArgumentException("A HTTPS site origin is required.", nameof(siteOrigin));

    endpoint = new Uri(siteOrigin, "/api/v1/mcp");
    client = new HttpClient(handler ?? new SocketsHttpHandler { UseCookies = false });
  }

  public Task<JsonElement> ListToolsAsync(string accessToken, CancellationToken cancellationToken = default) =>
      SendAsync("tools/list", new { }, accessToken, cancellationToken);

  public async Task<McpToolResult> CallToolAsync(
      string name,
      JsonElement arguments,
      string accessToken,
      CancellationToken cancellationToken = default)
  {
    var result = await SendAsync("tools/call", new { name, arguments }, accessToken, cancellationToken)
        .ConfigureAwait(false);
    var toolResult = result.Deserialize<McpToolResult>() ?? throw new InvalidDataException("Missing MCP tool result.");
    if (toolResult.IsError == true && toolResult.Content.ValueKind == JsonValueKind.Array)
    {
      foreach (var block in toolResult.Content.EnumerateArray())
      {
        if (block.ValueKind != JsonValueKind.Object ||
            !block.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
          continue;
        try
        {
          using var payload = JsonDocument.Parse(text.GetString()!);
          if (payload.RootElement.ValueKind != JsonValueKind.Object ||
              !payload.RootElement.TryGetProperty("error", out var error) ||
              error.ValueKind != JsonValueKind.Object ||
              !error.TryGetProperty("code", out var code) ||
              code.ValueKind != JsonValueKind.String || code.GetString() != "RATE_LIMIT")
            continue;
          TimeSpan? retryAfter = error.TryGetProperty("retryAfterSeconds", out var seconds) &&
              seconds.ValueKind == JsonValueKind.Number && seconds.TryGetInt32(out var count) && count > 0
              ? TimeSpan.FromSeconds(count) : null;
          throw new McpRateLimitException(retryAfter);
        }
        catch (JsonException)
        {
          // Other tool errors retain their original wrapped content for the model.
        }
      }
    }
    return toolResult;
  }

  private async Task<JsonElement> SendAsync(
      string method,
      object parameters,
      string accessToken,
      CancellationToken cancellationToken)
  {
    var id = Interlocked.Increment(ref nextRequestId).ToString(System.Globalization.CultureInfo.InvariantCulture);
    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
    {
      Content = new StringContent(
          JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters }),
          Encoding.UTF8,
          "application/json")
    };
    request.Headers.Accept.ParseAdd("application/json, text/event-stream");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if (response.StatusCode == HttpStatusCode.Unauthorized) throw new McpUnauthorizedException();
    if (response.StatusCode == HttpStatusCode.TooManyRequests)
    {
      var retry = response.Headers.RetryAfter;
      var delay = retry?.Delta ?? (retry?.Date - DateTimeOffset.UtcNow);
      throw new McpRateLimitException(delay > TimeSpan.Zero ? delay : null);
    }
    if (response.StatusCode != HttpStatusCode.OK)
      throw new HttpRequestException("MCP request failed.", null, response.StatusCode);

    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    var root = document.RootElement;
    if (!root.TryGetProperty("jsonrpc", out var version) || version.GetString() != "2.0" ||
        !root.TryGetProperty("id", out var responseId) || responseId.GetString() != id)
      throw new InvalidDataException("MCP response does not match the request.");
    if (root.TryGetProperty("error", out var error))
      throw new McpRpcException(error.GetProperty("code").GetInt32(), error.GetProperty("message").GetString() ?? "");
    if (!root.TryGetProperty("result", out var result))
      throw new InvalidDataException("MCP response has no result.");
    return result.Clone();
  }

  public void Dispose() => client.Dispose();
}

public sealed record McpToolResult(
    [property: System.Text.Json.Serialization.JsonPropertyName("content")] JsonElement Content,
    [property: System.Text.Json.Serialization.JsonPropertyName("structuredContent")] JsonElement? StructuredContent,
    [property: System.Text.Json.Serialization.JsonPropertyName("isError")] bool? IsError);
