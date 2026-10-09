using System.Text;
using System.Text.Json;

namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpClient
{
  private const int MaximumResponseChars = 1_048_576;

  private static async Task<JsonElement> ReadReplyAsync(
      HttpContent content, string id, CancellationToken cancellationToken)
  {
    var mediaType = content.Headers.ContentType?.MediaType;
    using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    using var reader = new StreamReader(stream, Encoding.UTF8);
    if (mediaType == "application/json")
    {
      var body = new StringBuilder();
      string? line;
      while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
      {
        if (body.Length + line.Length + 1 > MaximumResponseChars)
          throw new InvalidDataException("MCP response is too large.");
        body.AppendLine(line);
      }
      if (TryReadReply(body.ToString(), id, allowUnmatched: false, out var result)) return result;
      throw new InvalidDataException("MCP response does not match the request.");
    }
    if (mediaType != "text/event-stream")
      throw new InvalidDataException("Unsupported MCP response content type.");

    var data = new StringBuilder();
    string? eventLine;
    while ((eventLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
    {
      if (eventLine.Length == 0)
      {
        if (data.Length > 0)
        {
          if (TryReadReply(data.ToString(), id, allowUnmatched: true, out var result)) return result;
          data.Clear();
        }
      }
      else if (eventLine.StartsWith("data:", StringComparison.Ordinal))
      {
        var value = eventLine.AsSpan(5).TrimStart(' ');
        if (data.Length + value.Length + 1 > MaximumResponseChars)
          throw new InvalidDataException("MCP event is too large.");
        if (data.Length > 0) data.Append('\n');
        data.Append(value);
      }
    }
    if (data.Length > 0 && TryReadReply(data.ToString(), id, allowUnmatched: true, out var last))
      return last;
    throw new InvalidDataException("MCP event stream ended without a matching response.");
  }

  private static bool TryReadReply(string body, string id, bool allowUnmatched, out JsonElement result)
  {
    result = default;
    JsonDocument document;
    try { document = JsonDocument.Parse(body); }
    catch (JsonException error) { throw new InvalidDataException("MCP response is not JSON.", error); }
    using (document)
    {
      var root = document.RootElement;
      if (root.ValueKind != JsonValueKind.Object ||
          !root.TryGetProperty("jsonrpc", out var version) || version.ValueKind != JsonValueKind.String ||
          version.GetString() != "2.0")
        throw new InvalidDataException("MCP response has an invalid version.");
      if (allowUnmatched && root.TryGetProperty("method", out _)) return false;
      if (!root.TryGetProperty("id", out var responseId) || responseId.ValueKind != JsonValueKind.String)
      {
        if (allowUnmatched && !root.TryGetProperty("id", out _)) return false;
        throw new InvalidDataException("MCP response has an invalid ID.");
      }
      if (responseId.GetString() != id)
      {
        if (allowUnmatched) return false;
        throw new InvalidDataException("MCP response does not match the request.");
      }
      if (root.TryGetProperty("error", out var error))
      {
        if (error.ValueKind != JsonValueKind.Object ||
            !error.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Number ||
            !code.TryGetInt32(out var codeValue) ||
            !error.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.String)
          throw new InvalidDataException("MCP error is malformed.");
        throw new McpRpcException(codeValue, message.GetString() ?? "");
      }
      if (!root.TryGetProperty("result", out var payload))
        throw new InvalidDataException("MCP response has no result.");
      result = payload.Clone();
      return true;
    }
  }
}
