using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Voucha.Client.Core.Agent;

/// <summary>A bearer-only member MCP transport with no application session cookies.</summary>
public sealed partial class MemberMcpClient : IDisposable, IAsyncDisposable
{
  private readonly HttpClient client;
  private readonly Uri endpoint;
  private readonly TimeSpan responseTimeout;
  private long nextRequestId;
  private readonly SemaphoreSlim initializeGate = new(1, 1);
  private readonly object sessionGate = new();
  private string? protocolVersion;
  private string? sessionId;
  private string? sessionAccessToken;
  private bool initialized;
  private bool closed;

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "HttpClient owns and disposes the handler.")]
  public MemberMcpClient(
      Uri siteOrigin, HttpMessageHandler? handler = null, TimeSpan? responseTimeout = null)
  {
    ArgumentNullException.ThrowIfNull(siteOrigin);
    if (siteOrigin.Scheme != Uri.UriSchemeHttps || siteOrigin.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(siteOrigin.UserInfo) ||
        !string.IsNullOrEmpty(siteOrigin.Query) || !string.IsNullOrEmpty(siteOrigin.Fragment))
      throw new ArgumentException("A HTTPS site origin is required.", nameof(siteOrigin));

    endpoint = new Uri(siteOrigin, "/api/v1/mcp");
    this.responseTimeout = responseTimeout ?? TimeSpan.FromMinutes(2);
    if (this.responseTimeout <= TimeSpan.Zero)
      throw new ArgumentOutOfRangeException(nameof(responseTimeout));
    client = new HttpClient(handler ?? new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false });
  }

  public Task<JsonElement> ListToolsAsync(string accessToken, CancellationToken cancellationToken = default) =>
      ListToolsAsync(accessToken, null, cancellationToken);

  public Task<JsonElement> ListToolsAsync(
      string accessToken, string? cursor, CancellationToken cancellationToken = default) =>
      SendAsync("tools/list", cursor is null ? new { } : (object)new { cursor }, accessToken, cancellationToken);

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
    await EnsureInitializedAsync(accessToken, cancellationToken).ConfigureAwait(false);
    try
    {
      return await SendRpcAsync(method, parameters, accessToken, cancellationToken).ConfigureAwait(false);
    }
    catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.NotFound &&
        error.Message == "MCP session expired.")
    {
      await EnsureInitializedAsync(accessToken, cancellationToken).ConfigureAwait(false);
      return await SendRpcAsync(method, parameters, accessToken, cancellationToken).ConfigureAwait(false);
    }
  }

  private async Task<JsonElement> SendRpcAsync(
      string method, object parameters, string accessToken, CancellationToken cancellationToken)
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
    string? version;
    string? requestSession;
    lock (sessionGate)
    {
      ObjectDisposedException.ThrowIf(closed, this);
      if (method != "initialize" && !initialized)
        throw new HttpRequestException("MCP session expired.", null, HttpStatusCode.NotFound);
      version = protocolVersion;
      requestSession = sessionId;
      if (initialized && requestSession is not null && sessionId == requestSession)
        sessionAccessToken = accessToken;
    }
    if (version is not null) request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", version);
    if (requestSession is not null) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", requestSession);
    using var response = await client.SendAsync(
        request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
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
      throw new HttpRequestException("MCP session expired.", null, HttpStatusCode.NotFound);
    }
    if (response.StatusCode == HttpStatusCode.TooManyRequests)
    {
      var retry = response.Headers.RetryAfter;
      var delay = retry?.Delta ?? (retry?.Date - DateTimeOffset.UtcNow);
      throw new McpRateLimitException(delay > TimeSpan.Zero ? delay : null);
    }
    if (response.StatusCode != HttpStatusCode.OK)
      throw new HttpRequestException("MCP request failed.", null, response.StatusCode);

    string? replySession = requestSession;
    if (method == "initialize" && response.Headers.TryGetValues("Mcp-Session-Id", out var values))
    {
      var proposed = values.SingleOrDefault();
      if (string.IsNullOrEmpty(proposed) || proposed.Any(character => character < 0x21 || character > 0x7E))
        throw new InvalidDataException("MCP server returned an invalid session ID.");
      replySession = proposed;
      lock (sessionGate) sessionId = proposed;
    }
    using var responseBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    responseBudget.CancelAfter(responseTimeout);
    var result = await ReadReplyAsync(
        response.Content, id, accessToken, replySession, version, responseBudget.Token).ConfigureAwait(false);
    return result;
  }

  public void Dispose()
  {
    lock (sessionGate)
    {
      closed = true;
      initialized = false;
      protocolVersion = null;
      sessionId = null;
      sessionAccessToken = null;
    }
    client.Dispose();
    initializeGate.Dispose();
  }
}

public sealed record McpToolResult(
    [property: System.Text.Json.Serialization.JsonPropertyName("content")] JsonElement Content,
    [property: System.Text.Json.Serialization.JsonPropertyName("structuredContent")] JsonElement? StructuredContent,
    [property: System.Text.Json.Serialization.JsonPropertyName("isError")] bool? IsError);
