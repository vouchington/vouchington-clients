using Voucha.Client.Core.Api;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  public async Task SendAsync(string message, CancellationToken cancellationToken = default)
  {
    _ = await TrySendAsync(message, cancellationToken).ConfigureAwait(true);
  }

  public async Task<bool> TrySendAsync(string message, CancellationToken cancellationToken = default)
  {
    if (Interlocked.CompareExchange(ref sendInFlight, 1, 0) != 0) return false;
    OnPropertyChanged(nameof(CanSend));
    try
    {
      return await TrySendCoreAsync(message, cancellationToken).ConfigureAwait(true);
    }
    finally
    {
      Volatile.Write(ref sendInFlight, 0);
      OnPropertyChanged(nameof(CanSend));
    }
  }

  private async Task<bool> TrySendCoreAsync(string message, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(message);
    var trimmed = message.Trim();
    if (trimmed.Length == 0 || IsDeleted || IsStreaming) return false;
    if (RejectConflictingCompletedMcpTurn(trimmed)) return false;
    if (blockedToolRetryText == trimmed) return false;
    blockedToolRetryText = null;
    var selectedProvider = ProviderForPendingMcpRetry(trimmed);
    ILocalChatProvider selectedLocalProvider;
    try
    {
      var resolvedProvider = LocalProviderForSend(trimmed, selectedProvider);
      if (resolvedProvider is null)
      {
        ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetChatConversationLocalChatUnavailable);
        State = LoadState.Error;
        return false;
      }
      selectedLocalProvider = resolvedProvider;
      selectedProvider = selectedLocalProvider.Status;
      SelectedProviderStatus = selectedProvider;
    }
    catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
    {
      ErrorMessage = ex.Message;
      State = LoadState.Error;
      return false;
    }
    var conversationId = ConversationId;
    if (RejectInvalidatedCompletedMcpAccount(trimmed, selectedLocalProvider)) return false;
    var retry = RetryableTurn(conversationId, trimmed, selectedProvider, selectedLocalProvider);
    if (retry is null && !selectedProvider.IsAvailable)
    {
      ErrorMessage = selectedProvider.StatusText;
      State = LoadState.Error;
      return false;
    }
    var currentRequest = BeginMutation();
    var messageTime = DateTimeOffset.UtcNow;
    var localUserMessageId = retry?.Body.UserMessageId ?? Guid.CreateVersion7(messageTime).ToString();
    var localAssistantMessageId = retry?.Body.AssistantMessageId ?? Guid.CreateVersion7(messageTime.AddMilliseconds(1)).ToString();
    LocalLLMResponseInput[] localHistory = LocalLLMHistory();
    var userMessageInserted = false;
    var assistantMessageInserted = false;
    var localChatPersisted = false;
    try
    {
      if (conversationId is null)
      {
        var created = await chatService.CreateConversationAsync(
            new CreateChatConversationBody(string.IsNullOrWhiteSpace(Title) ? null : Title),
            cancellationToken).ConfigureAwait(true);
        if (currentRequest != Volatile.Read(ref requestId)) return false;
        conversationId = created.Conversation.Id;
        ConversationId = conversationId;
        Title = created.Conversation.Title;
      }

      if (retry is null && providerResolver is ILocalChatProviderResolver refreshedResolver)
      {
        selectedLocalProvider = refreshedResolver.GetLocalProvider(selectedProvider.ModelProvider ?? string.Empty)
            ?? throw new InvalidOperationException(
                localization.Localize(UiMessageKey.NativeDotnetChatConversationLocalChatUnavailable));
      }
      selectedProvider = selectedLocalProvider.Status;
      SelectedProviderStatus = selectedProvider;
      if (retry is null && !selectedProvider.IsAvailable)
      {
        ErrorMessage = selectedProvider.StatusText;
        State = LoadState.Error;
        return false;
      }

      messages.Add(new ChatMessageRow(
          localUserMessageId,
          "user",
          trimmed,
          DateTimeOffset.UtcNow));
      userMessageInserted = true;
      OnPropertyChanged(nameof(Messages));

      var localResult = await SendLocalTurnAsync(
          trimmed,
          conversationId,
          localUserMessageId,
          localAssistantMessageId,
          localHistory,
          selectedLocalProvider,
          selectedProvider,
          retry,
          currentRequest,
          cancellationToken).ConfigureAwait(true);
      assistantMessageInserted = localResult.Inserted;
      localChatPersisted = true;

      if (currentRequest != Volatile.Read(ref requestId)) return false;
      if (ConversationId is { Length: > 0 })
      {
        await ReloadMessagesAsync(ConversationId, currentRequest, cancellationToken,
            localResult.Pending).ConfigureAwait(true);
      }
      if (currentRequest != Volatile.Read(ref requestId)) return false;
      State = LoadState.Loaded;
      return true;
    }
    catch (PossibleMcpToolSideEffectException)
    {
      HandlePossibleToolSideEffect(currentRequest, localAssistantMessageId,
          assistantMessageInserted, localUserMessageId, userMessageInserted, trimmed);
      return false;
    }
    catch (OperationCanceledException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        if (!localChatPersisted)
        {
          if (assistantMessageInserted)
          {
            RemoveLocalMessage(localAssistantMessageId);
          }
          if (userMessageInserted)
          {
            RemoveLocalMessage(localUserMessageId);
          }
        }
        State = ConversationId is { Length: > 0 } ? LoadState.Loaded : LoadState.Idle;
        return localChatPersisted;
      }

      return false;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException or
        InvalidDataException or McpUnauthorizedException or McpRateLimitException or McpRpcException or
        System.Text.Json.JsonException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        if (!localChatPersisted)
        {
          if (assistantMessageInserted)
          {
            RemoveLocalMessage(localAssistantMessageId);
          }
          if (userMessageInserted) RemoveLocalMessage(localUserMessageId);
        }
        var isMcpProvider = selectedLocalProvider is IMcpLocalChatProvider;
        var hasMcpSnapshot = selectedLocalProvider is IMcpLocalChatProvider mcp &&
            (retry?.McpSnapshot is not null || mcp.ConnectedMcpSnapshot() is not null);
        ErrorMessage = PresentSendError(ex, isMcpProvider, hasMcpSnapshot);
        State = LoadState.Error;
      }

      return localChatPersisted;
    }
    finally
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        IsStreaming = false;
        var streamingTokenSource = Interlocked.Exchange(ref streamingCts, null);
        streamingTokenSource?.Dispose();
      }
    }
  }

  private LocalLLMResponseInput[] LocalLLMHistory() => LocalLLMHistoryBudget.Apply(messages);
}
