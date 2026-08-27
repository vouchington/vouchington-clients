using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  public async Task LoadAsync(string? conversationId, string? conversationTitle = null, CancellationToken cancellationToken = default)
  {
    if (ConversationId == conversationId && Messages.Count > 0 && State != LoadState.Error && !IsDeleted) return;

    Reset();
    ConversationId = conversationId;
    Title = conversationTitle ?? string.Empty;
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
