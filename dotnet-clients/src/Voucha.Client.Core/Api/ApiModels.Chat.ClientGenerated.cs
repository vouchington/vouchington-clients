using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record CreateClientGeneratedChatBody(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("assistant_content")] string AssistantContent,
    [property: JsonPropertyName("model_provider")] string ModelProvider,
    [property: JsonPropertyName("user_message_id")] string UserMessageId,
    [property: JsonPropertyName("assistant_message_id")] string AssistantMessageId,
    [property: JsonPropertyName("model_name")] string? ModelName = null);

public sealed record ClientGeneratedChatTurn(
    [property: JsonPropertyName("user_message_id")] string UserMessageId,
    [property: JsonPropertyName("assistant_message_id")] string AssistantMessageId);

public sealed record ClientGeneratedChatResponse(
    [property: JsonPropertyName("user_message")] ChatMessage UserMessage,
    [property: JsonPropertyName("assistant_message")] ChatMessage AssistantMessage,
    [property: JsonPropertyName("turn")] ClientGeneratedChatTurn Turn);

#pragma warning restore CA1054, CA1056, CA1720
