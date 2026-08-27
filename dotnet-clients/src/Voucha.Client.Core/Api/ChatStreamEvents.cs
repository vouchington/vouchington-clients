using System.Text.Json;

namespace Voucha.Client.Core.Api;

public abstract record ChatStreamEvent;

public sealed record ChatStreamMetadataEvent(
    string ConversationId,
    string UserMessageId,
    string AssistantMessageId,
    string JobId) : ChatStreamEvent;

public sealed record ChatStreamTextEvent(string Content) : ChatStreamEvent;

public sealed record ChatStreamToolCallEvent(
    string ToolCallId,
    string Name,
    string Arguments) : ChatStreamEvent;

public sealed record ChatStreamToolResultEvent(
    string ToolCallId,
    JsonElement Result) : ChatStreamEvent
{
  public string DisplayResult => Result.ToString();
}

public sealed record ChatStreamSubagentStepEvent(
    string AgentName,
    string ToolName,
    string? ToolCallId = null) : ChatStreamEvent;

public sealed record ChatStreamSubagentTextEvent(
    string AgentName,
    string Content,
    string? ToolCallId = null) : ChatStreamEvent;

public sealed record ChatStreamErrorEvent(string Error) : ChatStreamEvent;

public sealed record ChatStreamDoneEvent() : ChatStreamEvent;
