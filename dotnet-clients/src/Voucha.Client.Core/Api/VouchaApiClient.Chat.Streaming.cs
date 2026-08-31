using System.Net.Http.Json;
using System.Runtime.CompilerServices;

namespace Voucha.Client.Core.Api;

public sealed class ChatStreamIncompleteException : InvalidOperationException
{
  public ChatStreamIncompleteException() : base("Chat stream ended before a terminal event.") { }

  public ChatStreamIncompleteException(string message) : base(message) { }

  public ChatStreamIncompleteException(string message, Exception innerException)
      : base(message, innerException) { }
}

public sealed class ChatStreamFrameTooLargeException : InvalidOperationException
{
  public ChatStreamFrameTooLargeException() : base("Chat stream frame is too large.") { }

  public ChatStreamFrameTooLargeException(string message) : base(message) { }

  public ChatStreamFrameTooLargeException(string message, Exception innerException)
      : base(message, innerException) { }
}

public sealed partial class VouchaApiClient
{
  private const int MaximumChatStreamFrameCharacters = 1_024 * 1_024;
  public IAsyncEnumerable<ChatStreamEvent> StreamChatConversationAsync(
      string conversationId,
      string message,
      CancellationToken cancellationToken) =>
      StreamChatConversationAsync(conversationId, message, provider: null, cancellationToken);

  public async IAsyncEnumerable<ChatStreamEvent> StreamChatConversationAsync(
      string conversationId,
      string message,
      string? provider = null,
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    using var request = new HttpRequestMessage(
        HttpMethod.Post,
        BuildUri(VouchaApiEndpoints.StreamChatConversationMessage(conversationId, message, provider)));
    request.Content = provider is null
        ? JsonContent.Create(new { message }, options: VouchaApiJson.Options)
        : JsonContent.Create(new { message, provider }, options: VouchaApiJson.Options);

    using var response = await httpClient
        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
        .ConfigureAwait(false);
    if (!response.IsSuccessStatusCode)
    {
      var responseBody = response.Content is not null
          ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
          : null;
      throw new VouchaApiException(
          response.StatusCode,
          string.IsNullOrEmpty(responseBody) ? null : responseBody);
    }

    var contentType = response.Content?.Headers.ContentType?.MediaType;
    if (!string.Equals(contentType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException("Voucha API returned an unexpected SSE response content type.");
    }

    if (response.Content is null)
    {
      throw new InvalidOperationException("Voucha API returned an empty SSE response body.");
    }

    var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    using var reader = new StreamReader(responseStream);
    var lineReader = new CappedSseLineReader(reader, MaximumChatStreamFrameCharacters);

    string currentEventType = "message";
    var currentDataLines = new List<string>();
    var currentFrameCharacters = 0;

    while (true)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var line = await lineReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
      if (line is null)
      {
        if (currentDataLines.Count > 0 || currentEventType != "message")
        {
          if (TryParseChatStreamEvent(currentEventType, currentDataLines, out var streamEvent))
          {
            yield return streamEvent;
            if (streamEvent is ChatStreamDoneEvent or ChatStreamErrorEvent)
            {
              yield break;
            }
          }
        }

        throw new ChatStreamIncompleteException();
      }

      var normalizedLine = line;
      if (normalizedLine.Length == 0)
      {
        if (TryParseChatStreamEvent(currentEventType, currentDataLines, out var streamEvent))
        {
          yield return streamEvent;
          if (streamEvent is ChatStreamDoneEvent or ChatStreamErrorEvent)
          {
            yield break;
          }
        }

        currentEventType = "message";
        currentDataLines.Clear();
        currentFrameCharacters = 0;
        continue;
      }

      if (normalizedLine.StartsWith(':'))
      {
        continue;
      }

      var colonIndex = normalizedLine.IndexOf(':', StringComparison.Ordinal);
      var field = colonIndex < 0 ? normalizedLine : normalizedLine[..colonIndex];
      var rawValue = colonIndex < 0 ? string.Empty : normalizedLine[(colonIndex + 1)..];
      var value = rawValue.StartsWith(' ') ? rawValue[1..] : rawValue;

      if (field == "event")
      {
        currentEventType = string.IsNullOrWhiteSpace(value) ? "message" : value;
      }
      else if (field == "data")
      {
        checked { currentFrameCharacters += value.Length + 1; }
        if (currentFrameCharacters > MaximumChatStreamFrameCharacters)
        {
          throw new ChatStreamFrameTooLargeException();
        }
        currentDataLines.Add(value);
      }
    }
  }

  private static bool TryParseChatStreamEvent(
      string eventType,
      List<string> dataLines,
      out ChatStreamEvent streamEvent)
  {
    var rawData = dataLines.Count == 0 ? string.Empty : string.Join('\n', dataLines);
    ChatStreamEvent? parsed = eventType switch
    {
      "done" => new ChatStreamDoneEvent(),
      "error" => new ChatStreamErrorEvent(ParseChatError(rawData)),
      "metadata" when TryGetObject(rawData, out var metadata) &&
          TryGetString(metadata, "conversation_id", out var conversationId) &&
          TryGetString(metadata, "assistant_message_id", out var assistantMessageId) &&
          TryGetString(metadata, "user_message_id", out var userMessageId) &&
          TryGetString(metadata, "job_id", out var jobId) =>
          new ChatStreamMetadataEvent(conversationId, userMessageId, assistantMessageId, jobId),
      "text" or "message" when TryGetObject(rawData, out var text) &&
          TryGetString(text, "content", out var content) =>
          new ChatStreamTextEvent(content),
      "tool_call" when TryGetObject(rawData, out var toolCall) &&
          TryGetString(toolCall, "tool_call_id", out var toolCallId) &&
          TryGetString(toolCall, "name", out var name) &&
          TryGetString(toolCall, "arguments", out var arguments) =>
          new ChatStreamToolCallEvent(toolCallId, name, arguments),
      "tool_result" when TryGetObject(rawData, out var toolResult) &&
          TryGetString(toolResult, "tool_call_id", out var toolResultCallId) &&
          TryGetJsonElement(toolResult, "result", out var result) =>
          new ChatStreamToolResultEvent(toolResultCallId, result),
      "subagent_step" when TryGetObject(rawData, out var subagentStep) &&
          TryGetString(subagentStep, "agent_name", out var agentName) &&
          TryGetString(subagentStep, "tool_name", out var toolName) =>
          new ChatStreamSubagentStepEvent(
              agentName,
              toolName,
              TryGetString(subagentStep, "tool_call_id", out var stepToolCallId) ? stepToolCallId : null),
      "subagent_text" when TryGetObject(rawData, out var subagentText) &&
          TryGetString(subagentText, "agent_name", out var subagentTextAgent) &&
          TryGetString(subagentText, "content", out var subagentContent) =>
          new ChatStreamSubagentTextEvent(
              subagentTextAgent,
              subagentContent,
              TryGetString(subagentText, "tool_call_id", out var subagentToolCallId) ? subagentToolCallId : null),
      _ => null,
    };

    if (parsed is null)
    {
      streamEvent = default!;
      return false;
    }

    streamEvent = parsed;
    return true;
  }
}
