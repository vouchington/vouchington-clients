using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record ChatConversation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("created_by_id")] string CreatedById,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("updated_by_id")] string? UpdatedById,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt,
    [property: JsonPropertyName("deleted_by_id")] string? DeletedById);

[JsonConverter(typeof(ChatMessageContentConverter))]
public sealed record ChatMessageContent(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("error")] string? Error = null)
{
  public string DisplayText => Content ?? string.Empty;
}

public sealed record ChatMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("conversation_id")] string ConversationId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("created_by_id")] string CreatedById,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("updated_by_id")] string? UpdatedById,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt,
    [property: JsonPropertyName("deleted_by_id")] string? DeletedById,
    [property: JsonPropertyName("content")] ChatMessageContent Content);

public sealed record ChatConversationListResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<ChatConversation> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record ChatConversationResponse(
    [property: JsonPropertyName("conversation")] ChatConversation Conversation);

public sealed record ChatMessagesResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<ChatMessage> Results,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record CreateChatConversationBody(
    [property: JsonPropertyName("title")] string? Title = null);

public sealed record UpdateChatConversationTitleBody(
    [property: JsonPropertyName("title")] string Title);

internal sealed class ChatMessageContentConverter : JsonConverter<ChatMessageContent>
{
  public override ChatMessageContent Read(
      ref Utf8JsonReader reader,
      Type typeToConvert,
      JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.String)
    {
      return new ChatMessageContent("message", reader.GetString());
    }

    if (reader.TokenType != JsonTokenType.StartObject)
    {
      throw new JsonException("Expected chat message content to be a string or object.");
    }

    using var document = JsonDocument.ParseValue(ref reader);
    var root = document.RootElement;
    var role = root.TryGetProperty("role", out var roleProperty) && roleProperty.ValueKind == JsonValueKind.String
        ? roleProperty.GetString()
        : null;
    var content = root.TryGetProperty("content", out var contentProperty) && contentProperty.ValueKind == JsonValueKind.String
        ? contentProperty.GetString()
        : null;
    var error = root.TryGetProperty("error", out var errorProperty) && errorProperty.ValueKind == JsonValueKind.String
        ? errorProperty.GetString()
        : null;

    return new ChatMessageContent(role ?? "message", content, error);
  }

  public override void Write(
      Utf8JsonWriter writer,
      ChatMessageContent value,
      JsonSerializerOptions options)
  {
    writer.WriteStartObject();
    writer.WriteString("role", value.Role);
    if (value.Content is null)
    {
      writer.WriteNull("content");
    }
    else
    {
      writer.WriteString("content", value.Content);
    }

    if (value.Error is null)
    {
      writer.WriteNull("error");
    }
    else
    {
      writer.WriteString("error", value.Error);
    }

    writer.WriteEndObject();
  }
}

#pragma warning restore CA1054, CA1056, CA1720
