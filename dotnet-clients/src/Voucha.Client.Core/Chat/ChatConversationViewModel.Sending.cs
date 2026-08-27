using Voucha.Client.Core.Api;
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
    ArgumentNullException.ThrowIfNull(message);
    var trimmed = message.Trim();
    if (trimmed.Length == 0 || IsDeleted || IsStreaming) return false;

    var selectedProvider = SelectedProviderStatus;
    try
    {
      if (selectedProvider.Kind == ChatProviderKind.Local)
      {
        selectedProvider = providerResolver is ILocalChatProviderResolver localResolver
            ? localResolver.GetLocalProvider(selectedProvider.ModelProvider ?? string.Empty)?.Status ?? selectedProvider
            : localChatProvider.Status;
        SelectedProviderStatus = selectedProvider;
      }
      if (selectedProvider.Kind == ChatProviderKind.Local && !selectedProvider.IsAvailable)
      {
        ErrorMessage = selectedProvider.StatusText;
        State = LoadState.Error;
        return false;
      }
    }
    catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
    {
      ErrorMessage = ex.Message;
      State = LoadState.Error;
      return false;
    }

    var currentRequest = BeginMutation();
    var conversationId = ConversationId;
    var localUserMessageId = Guid.NewGuid().ToString("N");
    var localAssistantMessageId = Guid.NewGuid().ToString("N");
    LocalLLMResponseInput[] localHistory = selectedProvider.Kind == ChatProviderKind.Local ? LocalLLMHistory() : [];
    var streamAccepted = false;
    var streamFailed = false;
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

      messages.Add(new ChatMessageRow(
          localUserMessageId,
          "user",
          trimmed,
          DateTimeOffset.UtcNow));
      userMessageInserted = true;
      OnPropertyChanged(nameof(Messages));

      if (selectedProvider.Kind == ChatProviderKind.Local)
      {
        assistantMessageInserted = await SendLocalTurnAsync(
            trimmed,
            conversationId,
            localAssistantMessageId,
            localHistory,
            selectedProvider,
            currentRequest,
            cancellationToken).ConfigureAwait(true);
        localChatPersisted = true;
      }
      else
      {
        var hostedResult = await SendHostedTurnAsync(
            trimmed,
            conversationId,
            selectedProvider,
            currentRequest,
            () => streamAccepted = true,
            cancellationToken).ConfigureAwait(true);
        streamAccepted = hostedResult.StreamAccepted;
        streamFailed = hostedResult.StreamFailed;
        if (!hostedResult.Accepted) return streamAccepted;
      }

      if (currentRequest != Volatile.Read(ref requestId)) return false;
      if (selectedProvider.Kind != ChatProviderKind.Local &&
          string.IsNullOrWhiteSpace(Title) &&
          ConversationId is { Length: > 0 })
      {
        await GenerateTitleAsyncCore(currentRequest, suppressErrors: true, cancellationToken).ConfigureAwait(true);
      }

      if (currentRequest != Volatile.Read(ref requestId)) return false;
      if (ConversationId is { Length: > 0 })
      {
        await ReloadMessagesAsync(ConversationId, currentRequest, cancellationToken).ConfigureAwait(true);
      }
      if (currentRequest != Volatile.Read(ref requestId)) return false;
      StreamContent = null;
      streamingAssistantMessageId = null;
      State = LoadState.Loaded;
      return true;
    }
    catch (OperationCanceledException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        if (selectedProvider.Kind == ChatProviderKind.Local)
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

        if (ConversationId is { Length: > 0 })
        {
          if (!await ReloadMessagesAsync(ConversationId, currentRequest, CancellationToken.None)
              .ConfigureAwait(true))
          {
            return false;
          }
        }

        StreamContent = null;
        streamingAssistantMessageId = null;
        State = ConversationId is { Length: > 0 } ? LoadState.Loaded : LoadState.Idle;
        return streamAccepted;
      }

      return false;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        if (selectedProvider.Kind == ChatProviderKind.Local)
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
        }
        else if (!streamAccepted)
        {
          RemoveLocalMessage(localUserMessageId);
        }

        ApplySendFailure(ex, selectedProvider, streamAccepted);
      }

      return selectedProvider.Kind == ChatProviderKind.Local ? localChatPersisted : streamAccepted;
    }
    finally
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        IsStreaming = false;
        if (streamFailed) State = LoadState.Error;
        var streamingTokenSource = Interlocked.Exchange(ref streamingCts, null);
        streamingTokenSource?.Dispose();
      }
    }
  }

  private LocalLLMResponseInput[] LocalLLMHistory() => LocalLLMHistoryBudget.Apply(messages);
}
