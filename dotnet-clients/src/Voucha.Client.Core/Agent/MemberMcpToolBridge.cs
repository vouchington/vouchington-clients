using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Chat;

namespace Voucha.Client.Core.Agent;

public sealed record MemberMcpFunctionTool(string Name, string Description, JsonElement Parameters);
public sealed record MemberMcpValidatedResult(
    string Envelope,
    McpFeedPostReceipt? FeedPostFact,
    McpFeedRssItemReceipt? RssItemFact = null,
    McpFeedReferralLinkReceipt? ReferralLinkFact = null);

/// <summary>Validates one immutable server catalog before projecting it to a local model.</summary>
public sealed class MemberMcpToolBridge
{
  // The producer bounds the complete MCP result at 1 MiB. The native envelope adds
  // three fields; raw validated JSON preserves that bound without re-escaping Unicode.
  public const int MaximumEnvelopeBytes = 1_100_000;
  private readonly Dictionary<string, JsonElement> schemas = new(StringComparer.Ordinal);
  private readonly Dictionary<string, MemberMcpOutputContract> outputs = new(StringComparer.Ordinal);
  public IReadOnlyList<MemberMcpFunctionTool> Tools { get; }

  public MemberMcpToolBridge(JsonElement catalog)
  {
    if (Encoding.UTF8.GetByteCount(catalog.GetRawText()) > 512_000 ||
        catalog.ValueKind != JsonValueKind.Object ||
        !catalog.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array ||
        tools.GetArrayLength() > MemberMcpToolCatalog.MaximumTools ||
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
      var hasOutput = tool.TryGetProperty("outputSchema", out _);
      var hasMapping = tool.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object &&
          meta.TryGetProperty("voucha/optionOutputPointers", out _);
      if (hasMapping && !hasOutput || MemberMcpOutputContract.OptionNames(schema) is not null && (!hasOutput || !hasMapping))
        throw new InvalidDataException("Missing MCP selected output contract.");
      if (hasOutput) outputs.Add(name, new MemberMcpOutputContract(tool));
      projected.Add(new MemberMcpFunctionTool(name, description, copy));
    }
    Tools = projected;
  }

  public Task<string> ExecuteAsync(
      string name, string rawArguments, MemberMcpSession session,
      MemberMcpConnectedSnapshot snapshot, CancellationToken cancellationToken = default) =>
      ExecuteAsync(name, rawArguments, session, snapshot, null, cancellationToken);

  public async Task<string> ExecuteAsync(
      string name, string rawArguments, MemberMcpSession session,
      MemberMcpConnectedSnapshot snapshot, Action? onAdmitted,
      CancellationToken cancellationToken = default)
  {
    var result = await ExecuteValidatedResultAsync(name, rawArguments, session, snapshot,
        callId: null, onAdmitted, cancellationToken).ConfigureAwait(false);
    return result.Envelope;
  }

  public async Task<MemberMcpValidatedResult> ExecuteValidatedResultAsync(
      string name, string rawArguments, MemberMcpSession session,
      MemberMcpConnectedSnapshot snapshot, string? callId, Action? onAdmitted = null,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(session);
    ArgumentNullException.ThrowIfNull(snapshot);
    var arguments = ValidatedArguments(name, rawArguments);
    var result = await session.CallToolAsync(snapshot, name, arguments, onAdmitted, cancellationToken)
        .ConfigureAwait(false);
    if (outputs.TryGetValue(name, out var output)) output.Validate(result, arguments);
    var envelope = SerializeResultEnvelope(result);
    McpFeedPostReceipt? postFact = null;
    McpFeedRssItemReceipt? rssFact = null;
    McpFeedReferralLinkReceipt? referralFact = null;
    if (result.IsError != true && result.StructuredContent is { } structured && callId is { Length: > 0 } id)
    {
      postFact = McpFeedPostReceipt.FromValidatedResult(name, arguments, structured, snapshot.AccountId, id);
      rssFact = McpFeedRssItemReceipt.FromValidatedResult(name, arguments, structured, snapshot.AccountId, id);
      referralFact = McpFeedReferralLinkReceipt.FromValidatedResult(
          name, arguments, structured, snapshot.AccountId, id);
    }
    return new MemberMcpValidatedResult(envelope, postFact, rssFact, referralFact);
  }

  internal static string SerializeResultEnvelope(McpToolResult result)
  {
    if (result.Content.ValueKind != JsonValueKind.Array)
      throw new InvalidDataException("Invalid MCP tool result content.");
    using var envelopeBytes = new MemoryStream();
    using (var writer = new Utf8JsonWriter(envelopeBytes))
    {
      writer.WriteStartObject();
      writer.WritePropertyName("structuredContent");
      if (result.StructuredContent is { } structured)
        writer.WriteRawValue(structured.GetRawText());
      else writer.WriteNullValue();
      writer.WritePropertyName("content");
      writer.WriteRawValue(result.Content.GetRawText());
      writer.WriteBoolean("isError", result.IsError == true);
      writer.WriteEndObject();
    }
    if (envelopeBytes.Length > MaximumEnvelopeBytes)
      throw new InvalidDataException("MCP tool result too large.");
    return Encoding.UTF8.GetString(envelopeBytes.GetBuffer().AsSpan(0, (int)envelopeBytes.Length));
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
