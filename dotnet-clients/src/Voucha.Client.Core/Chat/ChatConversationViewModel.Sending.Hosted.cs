using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private async Task<HostedSendResult> SendHostedTurnAsync(
      string trimmed,
      string conversationId,
      ChatProviderStatus selectedProvider,
      int currentRequest,
      Action markStreamAccepted,
      CancellationToken cancellationToken)
  {
    ResetStreamingState();
    var streamingTokenSource = await ResetStreamingTokenAsync(cancellationToken).ConfigureAwait(true);

    var streamAccepted = false;
    await foreach (var streamEvent in chatService.StreamConversationAsync(
        conversationId,
        trimmed,
        selectedProvider.ModelProvider,
        streamingTokenSource.Token).ConfigureAwait(true))
    {
      if (currentRequest != Volatile.Read(ref requestId)) return new(false, streamAccepted, false);
      switch (streamEvent)
      {
        case ChatStreamMetadataEvent metadata:
          streamAccepted = true;
          markStreamAccepted();
          ConversationId = metadata.ConversationId;
          UpdateStreamingAssistantMessage(metadata.AssistantMessageId);
          break;
        case ChatStreamTextEvent text:
          StreamContent = string.Concat(StreamContent, text.Content);
          UpdateStreamingAssistantMessage(null);
          break;
        case ChatStreamToolCallEvent toolCall:
          toolCalls.Add(toolCall);
          OnPropertyChanged(nameof(ToolCalls));
          break;
        case ChatStreamToolResultEvent toolResult:
          toolResults.Add(toolResult);
          OnPropertyChanged(nameof(ToolResults));
          break;
        case ChatStreamSubagentStepEvent subagentStep:
          subagentSteps.Add(subagentStep);
          OnPropertyChanged(nameof(SubagentSteps));
          break;
        case ChatStreamSubagentTextEvent subagentText:
          subagentTextChunks.Add(subagentText);
          OnPropertyChanged(nameof(SubagentTextChunks));
          break;
        case ChatStreamErrorEvent error:
          ErrorMessage = error.Error;
          MarkStreamingAssistantMessageFailed(error.Error);
          State = LoadState.Error;
          return new(false, streamAccepted, true);
        case ChatStreamDoneEvent:
          break;
      }
    }

    return new(true, streamAccepted, false);
  }

  private void ResetStreamingState()
  {
    StreamContent = string.Empty;
    IsStreaming = true;
    streamingAssistantMessageId = null;
    toolCalls.Clear();
    toolResults.Clear();
    subagentSteps.Clear();
    subagentTextChunks.Clear();
    OnPropertyChanged(nameof(ToolCalls));
    OnPropertyChanged(nameof(ToolResults));
    OnPropertyChanged(nameof(SubagentSteps));
    OnPropertyChanged(nameof(SubagentTextChunks));
  }

  private async Task<CancellationTokenSource> ResetStreamingTokenAsync(CancellationToken cancellationToken)
  {
    var previousStreamingTokenSource = Interlocked.Exchange(ref streamingCts, null);
    if (previousStreamingTokenSource is not null)
    {
      await previousStreamingTokenSource.CancelAsync().ConfigureAwait(true);
      previousStreamingTokenSource.Dispose();
    }

    var nextStreamingTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var replacedStreamingTokenSource = Interlocked.Exchange(ref streamingCts, nextStreamingTokenSource);
    if (replacedStreamingTokenSource is not null)
    {
      await replacedStreamingTokenSource.CancelAsync().ConfigureAwait(true);
      replacedStreamingTokenSource.Dispose();
    }

    return nextStreamingTokenSource;
  }

  private readonly record struct HostedSendResult(
      bool Accepted,
      bool StreamAccepted,
      bool StreamFailed);
}
