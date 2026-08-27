using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

#pragma warning disable CA1054, CA1056, CA1720

public sealed record CreateClientGeneratedChatBody(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("assistant_content")] string AssistantContent,
    [property: JsonPropertyName("model_provider")] string ModelProvider,
    [property: JsonPropertyName("model_name")] string? ModelName = null);

public sealed record ClientGeneratedChatAgenticRun(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("conversation_id")] string ConversationId,
    [property: JsonPropertyName("conversation_message_id")] string ConversationMessageId,
    [property: JsonPropertyName("parent_agentic_run_id")] string? ParentAgenticRunId,
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("model_provider")] string ModelProvider,
    [property: JsonPropertyName("input")] JsonElement Input,
    [property: JsonPropertyName("output")] JsonElement? Output,
    [property: JsonPropertyName("error")] JsonElement? Error,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("termination_reason")] string? TerminationReason,
    [property: JsonPropertyName("started_at")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("completed_at")] DateTimeOffset? CompletedAt,
    [property: JsonPropertyName("failed_at")] DateTimeOffset? FailedAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("deleted_at")] DateTimeOffset? DeletedAt);

public sealed record ClientGeneratedChatResponse(
    [property: JsonPropertyName("user_message")] ChatMessage UserMessage,
    [property: JsonPropertyName("assistant_message")] ChatMessage AssistantMessage,
    [property: JsonPropertyName("agentic_run")] ClientGeneratedChatAgenticRun AgenticRun);

#pragma warning restore CA1054, CA1056, CA1720
