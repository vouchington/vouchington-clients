using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  public async Task LoadAsync(string? conversationId, string? conversationTitle = null, CancellationToken cancellationToken = default)
  {
    if (ConversationId == conversationId && Messages.Count > 0 && State != LoadState.Error && !IsDeleted) return;

    var retainedTurn = !IsDeleted && conversationId == ConversationId && pendingLocalTurn?.ConversationId == conversationId
        ? pendingLocalTurn : null;
    var retainedProvider = retainedTurn is null ? null : SelectedProviderStatus;
    Reset();
    ConversationId = conversationId;
    Title = conversationTitle ?? string.Empty;
    if (retainedTurn is not null)
    {
      SelectedProviderStatus = retainedProvider!;
      pendingLocalTurn = retainedTurn;
    }
    if (conversationId is null)
    {
      State = LoadState.Loaded;
      return;
    }

    State = LoadState.Loading;
    var currentRequest = Volatile.Read(ref requestId);
    try
    {
      var reloaded = await ReloadMessagesAsync(conversationId, currentRequest, cancellationToken).ConfigureAwait(true);
      if (reloaded && currentRequest == Volatile.Read(ref requestId))
      {
        if (pendingLocalTurn is { } pending &&
            messages.Any(message => message.Id == pending.Body.UserMessageId) &&
            messages.Any(message => message.Id == pending.Body.AssistantMessageId))
        {
          pendingLocalTurn = null;
        }
        State = LoadState.Loaded;
      }
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
      throw;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (currentRequest == Volatile.Read(ref requestId))
      {
        ErrorMessage = ex.Message;
        State = LoadState.Error;
      }
    }
  }
}
