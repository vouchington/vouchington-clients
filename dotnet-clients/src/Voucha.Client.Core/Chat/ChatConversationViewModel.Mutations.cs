using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  public async Task RenameAsync(string newTitle, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(newTitle);
    if (ConversationId is null || IsDeleted) return;

    var trimmed = newTitle.Trim();
    var previous = Title;
    Title = trimmed;
    var currentRequest = BeginMutation();
    try
    {
      var updated = await chatService.UpdateConversationTitleAsync(
          ConversationId,
          trimmed,
          cancellationToken).ConfigureAwait(true);
      if (currentRequest != Volatile.Read(ref requestId)) return;
      Title = updated.Conversation.Title;
      State = LoadState.Loaded;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        Title = previous;
        ErrorMessage = ex.Message;
        State = LoadState.Error;
      }
    }
  }

  public async Task GenerateTitleAsync(
      bool suppressErrors = false,
      CancellationToken cancellationToken = default)
  {
    if (ConversationId is null || IsDeleted) return;

    var currentRequest = BeginMutation();
    await GenerateTitleAsyncCore(currentRequest, suppressErrors, cancellationToken).ConfigureAwait(true);
  }

  public async Task DeleteAsync(CancellationToken cancellationToken = default)
  {
    if (ConversationId is null || IsDeleted) return;

    await StopStreamingAsync().ConfigureAwait(true);
    var currentRequest = BeginMutation();
    var wasDeleted = IsDeleted;
    IsDeleted = true;
    try
    {
      await chatService.DeleteConversationAsync(ConversationId, cancellationToken).ConfigureAwait(true);
      if (currentRequest != Volatile.Read(ref requestId)) return;
      State = LoadState.Loaded;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        IsDeleted = wasDeleted;
        ErrorMessage = ex.Message;
        State = LoadState.Error;
      }
    }
  }

  public void Reset()
  {
    _ = Interlocked.Increment(ref requestId);
    var cts = Interlocked.Exchange(ref streamingCts, null);
    if (cts is not null) { cts.Cancel(); cts.Dispose(); }
    messages.Clear();
    toolCalls.Clear();
    toolResults.Clear();
    subagentSteps.Clear();
    subagentTextChunks.Clear();
    streamingAssistantMessageId = null;
    ConversationId = null;
    Title = string.Empty;
    ErrorMessage = null;
    StreamContent = null;
    HasMoreMessages = false;
    nextMessageCursor = null;
    _ = Interlocked.Increment(ref olderMessagesRequestId);
    IsLoadingOlderMessages = false;
    OlderMessagesErrorMessage = null;
    IsDeleted = false;
    IsStreaming = false;
    State = LoadState.Idle;
    SelectedProviderStatus = providerResolver.GetDefaultProviderStatus();
    OnPropertyChanged(nameof(Messages));
    OnPropertyChanged(nameof(ToolCalls));
    OnPropertyChanged(nameof(ToolResults));
    OnPropertyChanged(nameof(SubagentSteps));
    OnPropertyChanged(nameof(SubagentTextChunks));
  }

  private int BeginMutation()
  {
    var currentRequest = Interlocked.Increment(ref requestId);
    ErrorMessage = null;
    State = LoadState.Loading;
    return currentRequest;
  }

  private async Task<bool> ReloadMessagesAsync(string conversationId, int currentRequest, CancellationToken cancellationToken)
  {
    var response = await chatService.FetchConversationMessagesAsync(conversationId, cancellationToken).ConfigureAwait(true);
    if (currentRequest != Volatile.Read(ref requestId)) return false;
    messages.Clear();
    messages.AddRange(response.Results.Select(MapMessage));
    HasMoreMessages = response.PageInfo.HasNextPage || response.PageInfo.HasMore == true;
    nextMessageCursor = response.PageInfo.EndCursor;
    OnPropertyChanged(nameof(CanLoadOlderMessages));
    OnPropertyChanged(nameof(Messages));
    return true;
  }
  private void RemoveLocalMessage(string messageId)
  {
    var index = messages.FindIndex(message => message.Id == messageId);
    if (index < 0) return;
    messages.RemoveAt(index);
    OnPropertyChanged(nameof(Messages));
  }

  private async Task GenerateTitleAsyncCore(
      int? currentRequest,
      bool suppressErrors,
      CancellationToken cancellationToken)
  {
    if (ConversationId is null || IsDeleted) return;

    var previous = Title;
    try
    {
      var updated = await chatService.GenerateConversationTitleAsync(
          ConversationId,
          cancellationToken).ConfigureAwait(true);
      if (currentRequest is not null && currentRequest.Value != Volatile.Read(ref requestId)) return;
      Title = updated.Conversation.Title;
      State = LoadState.Loaded;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (currentRequest is null || currentRequest.Value == Volatile.Read(ref requestId))
      {
        Title = previous;
        if (!suppressErrors)
        {
          ErrorMessage = ex.Message;
          State = LoadState.Error;
        }
      }
    }
  }

  private void UpdateStreamingAssistantMessage(string? assistantMessageId)
  {
    var content = StreamContent ?? string.Empty;
    var nextMessageId = assistantMessageId ?? streamingAssistantMessageId ?? Guid.NewGuid().ToString("N");
    var currentMessageId = streamingAssistantMessageId;
    if (currentMessageId is not null)
    {
      var currentIndex = messages.FindLastIndex(row => row.Role == "assistant" && row.Id == currentMessageId);
      if (currentIndex >= 0)
      {
        messages[currentIndex] = new ChatMessageRow(nextMessageId, "assistant", content, DateTimeOffset.UtcNow);
        streamingAssistantMessageId = nextMessageId;
        OnPropertyChanged(nameof(Messages));
        return;
      }
    }

    var existingIndex = messages.FindLastIndex(row => row.Role == "assistant" && row.Id == nextMessageId);
    if (existingIndex >= 0)
    {
      messages[existingIndex] = messages[existingIndex] with { Content = content };
    }
    else
    {
      messages.Add(new ChatMessageRow(nextMessageId, "assistant", content, DateTimeOffset.UtcNow));
    }

    streamingAssistantMessageId = nextMessageId;
    OnPropertyChanged(nameof(Messages));
  }

}
