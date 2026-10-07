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
      ChatProviderStatus selectedProvider,
      int currentRequest,
      CancellationToken cancellationToken)
  {
    ResetStreamingState();
    var streamingTokenSource = await ResetStreamingTokenAsync(cancellationToken).ConfigureAwait(true);

    var provider = providerResolver is ILocalChatProviderResolver localResolver
        ? localResolver.GetLocalProvider(selectedProvider.ModelProvider ?? string.Empty) ?? localChatProvider
        : localChatProvider;
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
}
