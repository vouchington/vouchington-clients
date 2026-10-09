using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private async Task<bool> SendLocalTurnAsync(
      string trimmed,
      string conversationId,
      string localUserMessageId,
      string localAssistantMessageId,
      IReadOnlyList<LocalLLMResponseInput> history,
      ILocalChatProvider provider,
      int currentRequest,
      CancellationToken cancellationToken)
  {
    ResetStreamingState();
    var streamingTokenSource = await ResetStreamingTokenAsync(cancellationToken).ConfigureAwait(true);

    var generation = await provider.GenerateAssistantContentAsync(
        trimmed,
        history,
        streamingTokenSource.Token).ConfigureAwait(true);
    if (currentRequest != Volatile.Read(ref requestId)) return false;

    messages.Add(new ChatMessageRow(
        localAssistantMessageId,
        "assistant",
        generation.AssistantContent,
        DateTimeOffset.UtcNow));
    OnPropertyChanged(nameof(Messages));

    try
    {
      await chatService.CreateClientGeneratedChatAsync(
          conversationId,
          new CreateClientGeneratedChatBody(
              trimmed,
              generation.AssistantContent,
              generation.ModelProvider,
              localUserMessageId,
              localAssistantMessageId,
              generation.ModelName),
          streamingTokenSource.Token).ConfigureAwait(true);
    }
    catch
    {
      RemoveLocalMessage(localAssistantMessageId);
      throw;
    }

    return true;
  }

  private void ResetStreamingState() => IsStreaming = true;

  private async Task<CancellationTokenSource> ResetStreamingTokenAsync(CancellationToken cancellationToken)
  {
    var previous = Interlocked.Exchange(ref streamingCts, null);
    if (previous is not null)
    {
      await previous.CancelAsync().ConfigureAwait(true);
      previous.Dispose();
    }

    var next = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var replaced = Interlocked.Exchange(ref streamingCts, next);
    if (replaced is not null)
    {
      await replaced.CancelAsync().ConfigureAwait(true);
      replaced.Dispose();
    }

    return next;
  }
}
