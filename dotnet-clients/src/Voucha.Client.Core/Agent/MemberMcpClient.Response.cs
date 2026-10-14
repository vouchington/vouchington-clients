using System.Text;
using System.Text.Json;

namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpClient
{
  // A producer-bounded 1 MiB result arrives inside a JSON-RPC response envelope.
  private const int MaximumResponseChars = 1_100_000;

  private async Task<JsonElement> ReadReplyAsync(
      HttpContent content, string id, string accessToken, string? requestSession,
      string? version, CancellationToken cancellationToken)
  {
    var mediaType = content.Headers.ContentType?.MediaType;
    using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    using var reader = new StreamReader(stream, Encoding.UTF8);
    if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
    {
      var body = new StringBuilder();
      string? line;
      while ((line = await ReadBoundedLineAsync(reader, cancellationToken).ConfigureAwait(false)) is not null)
      {
        if (body.Length + line.Length + 1 > MaximumResponseChars)
          throw new InvalidDataException("MCP response is too large.");
        body.AppendLine(line);
      }
      if (TryReadReply(body.ToString(), id, allowUnmatched: false, out var result)) return result;
      throw new InvalidDataException("MCP response does not match the request.");
    }
    if (!string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException("Unsupported MCP response content type.");

    var data = new StringBuilder();
    string? eventLine;
    while ((eventLine = await ReadBoundedLineAsync(reader, cancellationToken).ConfigureAwait(false)) is not null)
    {
      if (eventLine.Length == 0)
      {
        if (data.Length > 0)
        {
          if (await ReplyToServerRequestAsync(
                  data.ToString(), accessToken, requestSession, version, cancellationToken).ConfigureAwait(false))
          {
            data.Clear();
            continue;
          }
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
    if (data.Length > 0)
    {
      if (await ReplyToServerRequestAsync(
              data.ToString(), accessToken, requestSession, version, cancellationToken).ConfigureAwait(false))
        throw new InvalidDataException("MCP event stream ended without a matching response.");
      if (TryReadReply(data.ToString(), id, allowUnmatched: true, out var last)) return last;
    }
    throw new InvalidDataException("MCP event stream ended without a matching response.");
  }

  private static async Task<string?> ReadBoundedLineAsync(
      StreamReader reader, CancellationToken cancellationToken)
  {
    var line = new StringBuilder();
    var character = new char[1];
    while (await reader.ReadAsync(character.AsMemory(), cancellationToken).ConfigureAwait(false) != 0)
    {
      if (character[0] == '\n') return line.ToString().TrimEnd('\r');
      if (line.Length == MaximumResponseChars)
        throw new InvalidDataException("MCP response line is too large.");
      line.Append(character[0]);
    }
    return line.Length == 0 ? null : line.ToString();
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
