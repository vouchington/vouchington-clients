using System.Text;
using System.Text.Json;

namespace Voucha.Client.Core.Agent;

public sealed record MemberMcpFunctionTool(string Name, string Description, JsonElement Parameters);

/// <summary>Validates one immutable server catalog before projecting it to a local model.</summary>
public sealed class MemberMcpToolBridge
{
  private readonly Dictionary<string, JsonElement> schemas = new(StringComparer.Ordinal);
  public IReadOnlyList<MemberMcpFunctionTool> Tools { get; }

  public MemberMcpToolBridge(JsonElement catalog)
  {
    if (Encoding.UTF8.GetByteCount(catalog.GetRawText()) > 512_000 ||
        catalog.ValueKind != JsonValueKind.Object ||
        !catalog.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array ||
        tools.GetArrayLength() > 128 ||
        catalog.TryGetProperty("nextCursor", out var cursor) && cursor.ValueKind != JsonValueKind.Null)
      throw new InvalidDataException("Invalid or incomplete MCP tool catalog.");
    var projected = new List<MemberMcpFunctionTool>();
    foreach (var tool in tools.EnumerateArray())
    {
      if (tool.ValueKind != JsonValueKind.Object ||
          !tool.TryGetProperty("name", out var nameValue) || nameValue.ValueKind != JsonValueKind.String ||
          nameValue.GetString() is not { Length: > 0 } name ||
          name.Length > 64 || !name.All(character =>
              char.IsAsciiLetterOrDigit(character) || character is '_' or '-') ||
          schemas.ContainsKey(name) ||
          !tool.TryGetProperty("inputSchema", out var schema) || schema.ValueKind != JsonValueKind.Object ||
          Encoding.UTF8.GetByteCount(schema.GetRawText()) > 32_000)
        throw new InvalidDataException("Invalid MCP tool definition.");
      var description = tool.TryGetProperty("description", out var described) &&
          described.ValueKind == JsonValueKind.String ? described.GetString()! : string.Empty;
      if (Encoding.UTF8.GetByteCount(description) > 8_192)
        throw new InvalidDataException("MCP tool description too large.");
      MemberMcpInputSchema.Inspect(schema);
      var copy = schema.Clone();
      schemas.Add(name, copy);
      projected.Add(new MemberMcpFunctionTool(name, description, copy));
    }
    Tools = projected;
  }

  public async Task<string> ExecuteAsync(
      string name, string rawArguments, MemberMcpSession session,
      MemberMcpConnectedSnapshot snapshot, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(session);
    ArgumentNullException.ThrowIfNull(snapshot);
    var arguments = ValidatedArguments(name, rawArguments);
    var result = await session.CallToolAsync(snapshot, name, arguments, cancellationToken)
        .ConfigureAwait(false);
    var envelope = JsonSerializer.Serialize(new
    {
      structuredContent = result.StructuredContent,
      content = result.Content,
      isError = result.IsError == true,
    });
    if (Encoding.UTF8.GetByteCount(envelope) > 256_000)
      throw new InvalidDataException("MCP tool result too large.");
    return envelope;
  }

  public void Validate(string name, string rawArguments) => _ = ValidatedArguments(name, rawArguments);

  private JsonElement ValidatedArguments(string name, string rawArguments)
  {
    if (!schemas.TryGetValue(name, out var schema)) throw new InvalidDataException("Unknown MCP tool.");
    if (Encoding.UTF8.GetByteCount(rawArguments) > 64_000)
      throw new InvalidDataException("MCP tool arguments too large.");
    using var parsed = JsonDocument.Parse(rawArguments, new JsonDocumentOptions { MaxDepth = 32 });
    if (parsed.RootElement.ValueKind != JsonValueKind.Object ||
        !MemberMcpInputSchema.Accepts(parsed.RootElement, schema))
      throw new InvalidDataException("Invalid MCP tool arguments.");
    return parsed.RootElement.Clone();
  }
}
