namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private bool isLoadingOlderMessages;
  private string? olderMessagesErrorMessage;
  private int olderMessagesRequestId;

  public bool CanLoadOlderMessages => HasMoreMessages && !isLoadingOlderMessages;

  public bool ShowLoadOlderMessages => HasMoreMessages && !HasOlderMessagesError;

  public bool IsLoadingOlderMessages
  {
    get => isLoadingOlderMessages;
    private set
    {
      if (SetProperty(ref isLoadingOlderMessages, value))
      {
        OnPropertyChanged(nameof(CanLoadOlderMessages));
      }
    }
  }

  public string? OlderMessagesErrorMessage
  {
    get => olderMessagesErrorMessage;
    private set
    {
      if (SetProperty(ref olderMessagesErrorMessage, value))
      {
        OnPropertyChanged(nameof(ExternalContentOlderMessagesErrorMessage));
        OnPropertyChanged(nameof(HasOlderMessagesError));
        OnPropertyChanged(nameof(ShowLoadOlderMessages));
      }
    }
  }

  public string? ExternalContentOlderMessagesErrorMessage => OlderMessagesErrorMessage;

  public bool HasOlderMessagesError => !string.IsNullOrWhiteSpace(OlderMessagesErrorMessage);

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "This UI command must convert every non-cancellation continuation failure into retry state.")]
  public async Task LoadOlderMessagesAsync(CancellationToken cancellationToken = default)
  {
    if (ConversationId is not { } id || nextMessageCursor is not { } after || !CanLoadOlderMessages) return;
    var currentRequest = Volatile.Read(ref requestId);
    var pageRequest = Interlocked.Increment(ref olderMessagesRequestId);
    IsLoadingOlderMessages = true;
    OlderMessagesErrorMessage = null;
    try
    {
      var response = await chatService.FetchConversationMessagesPageAsync(id, after, 50, cancellationToken).ConfigureAwait(true);
      if (!IsCurrentOlderMessagesRequest(currentRequest, pageRequest, id)) return;
      var existingIds = messages.Select(message => message.Id).ToHashSet(StringComparer.Ordinal);
      var olderMessages = response.Results.Select(MapMessage).Where(message => existingIds.Add(message.Id)).ToArray();
      messages.InsertRange(0, olderMessages);
      HasMoreMessages = response.PageInfo.HasNextPage || response.PageInfo.HasMore == true;
      nextMessageCursor = response.PageInfo.EndCursor;
      OnPropertyChanged(nameof(Messages));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
      if (IsCurrentOlderMessagesRequest(currentRequest, pageRequest, id))
      {
        OlderMessagesErrorMessage = ex.Message;
      }
    }
    finally
    {
      if (pageRequest == Volatile.Read(ref olderMessagesRequestId))
      {
        IsLoadingOlderMessages = false;
      }
    }
  }

  private bool IsCurrentOlderMessagesRequest(int conversationRequest, int pageRequest, string id) =>
      conversationRequest == Volatile.Read(ref requestId) &&
      pageRequest == Volatile.Read(ref olderMessagesRequestId) &&
      string.Equals(ConversationId, id, StringComparison.Ordinal);
}
