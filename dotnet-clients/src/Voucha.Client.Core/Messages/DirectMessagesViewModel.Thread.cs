using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Messages;

public sealed partial class DirectMessagesViewModel
{
  public async Task SelectConversationAsync(string conversationId, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(conversationId)) return;
    var loadGeneration = BeginSelectedConversationLoad();
    SelectedConversationId = conversationId;
    Messages = [];
    Participants = [];
    ParticipantAddPolicy = "owner_only";
    messageCursor = null;
    HasMoreMessages = true;
    ThreadState = LoadState.Loading;
    ThreadErrorMessage = null;
    ResetParticipantSearchState();
    try
    {
      var conversationTask = messagesService.FetchDirectConversationAsync(conversationId, cancellationToken);
      var messagesTask = messagesService.FetchDirectConversationMessagesAsync(
          new FetchDirectConversationMessagesRequest(conversationId), cancellationToken);
      var participantsTask = messagesService.FetchDirectConversationParticipantsAsync(conversationId, cancellationToken);
      await Task.WhenAll(conversationTask, messagesTask, participantsTask).ConfigureAwait(true);

      if (!IsCurrentSelectedConversationLoad(conversationId, loadGeneration))
      {
        return;
      }

      var conversation = await conversationTask.ConfigureAwait(true);
      var messagePage = await messagesTask.ConfigureAwait(true);
      var participantPage = await participantsTask.ConfigureAwait(true);
      if (!IsCurrentSelectedConversationLoad(conversationId, loadGeneration))
      {
        return;
      }
      var rawParticipants = participantPage.Results;
      var isOwner = rawParticipants.Any(participant =>
          string.Equals(participant.UserId, currentUserId, StringComparison.Ordinal) &&
          string.Equals(participant.Role, "owner", StringComparison.Ordinal));
      ParticipantAddPolicy = conversation.Conversation.ParticipantAddPolicy ?? "owner_only";
      CompleteMessagePage(messagePage, replace: true);
      Participants = rawParticipants.Select(participant => ToRow(participant, isOwner)).ToArray();
      ThreadState = LoadState.Loaded;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (!IsCurrentSelectedConversationLoad(conversationId, loadGeneration))
      {
        return;
      }

      ThreadErrorMessage = ex.Message;
      ThreadState = LoadState.Error;
    }
  }

  public async Task LoadMoreMessagesAsync(CancellationToken cancellationToken = default)
  {
    if (SelectedConversationId is null ||
        !HasMoreMessages ||
        ThreadState == LoadState.Loading ||
        messageCursor is null)
    {
      return;
    }

    var conversationId = SelectedConversationId;
    var cursor = messageCursor;
    var loadGeneration = selectedConversationLoadGeneration;
    if (HasOlderMessagesLoadInProgress(conversationId, loadGeneration, cursor))
    {
      return;
    }

    olderMessagesLoading = true;
    olderMessagesLoadingConversationId = conversationId;
    olderMessagesLoadingGeneration = loadGeneration;
    olderMessagesLoadingCursor = cursor;
    ThreadErrorMessage = null;
    try
    {
      var response = await messagesService
          .FetchDirectConversationMessagesAsync(
              new FetchDirectConversationMessagesRequest(conversationId, cursor),
              cancellationToken)
          .ConfigureAwait(true);
      if (!IsCurrentOlderMessagesLoad(conversationId, loadGeneration, cursor))
      {
        return;
      }
      CompleteMessagePage(response, replace: false);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (!IsCurrentOlderMessagesLoad(conversationId, loadGeneration, cursor))
      {
        return;
      }

      ThreadErrorMessage = ex.Message;
      ThreadState = LoadState.Error;
    }
    finally
    {
      if (HasOlderMessagesLoadInProgress(conversationId, loadGeneration, cursor))
      {
        ClearOlderMessagesLoadingState();
      }
    }
  }

  public async Task<bool> SendMessageAsync(string text, CancellationToken cancellationToken = default)
  {
    var trimmed = text?.Trim() ?? string.Empty;
    var conversationId = SelectedConversationId;
    if (conversationId is null || trimmed.Length == 0 || !pendingSendConversationIds.Add(conversationId)) return false;
    var optimistic = new DirectMessageRow(
        $"optimistic-{Guid.NewGuid():N}",
        trimmed,
        UiText.Localized(UiMessageKey.NativeDotnetDirectMessagesYou),
        DateTimeOffset.UtcNow,
        true,
        localization);
    Messages = [.. Messages, optimistic];
    try
    {
      var response = await messagesService.SendDirectMessageAsync(
          new SendDirectMessageRequest(conversationId, trimmed), cancellationToken)
          .ConfigureAwait(true);

      var isStillSelected = string.Equals(SelectedConversationId, conversationId, StringComparison.Ordinal);
      var responseRow = ToRow(response.Message, optimistic.SenderText);
      if (isStillSelected && Messages.Any(row => row.Id == optimistic.Id))
      {
        Messages = Messages.Select(row => row.Id == optimistic.Id ? responseRow : row).ToArray();
      }
      else if (isStillSelected && !Messages.Any(row => row.Id == responseRow.Id))
      {
        Messages = [.. Messages, responseRow];
      }

      if (isStillSelected)
      {
        await ReloadSelectedThreadAsync(cancellationToken).ConfigureAwait(true);
      }
      await ReloadInboxAsync(cancellationToken).ConfigureAwait(true);
      BumpConversationInboxRowIfReloadFailed(conversationId, response.Message.UpdatedAt ?? response.Message.CreatedAt);
      return isStillSelected;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (!string.Equals(SelectedConversationId, conversationId, StringComparison.Ordinal))
      {
        return false;
      }

      Messages = Messages.Where(row => row.Id != optimistic.Id).ToArray();
      ThreadErrorMessage = ex.Message;
      ThreadState = LoadState.Error;
      return false;
    }
    finally
    {
      pendingSendConversationIds.Remove(conversationId);
    }
  }

  private void CompleteMessagePage(DirectMessagesResponse response, bool replace)
  {
    var rows = response.Results.Select(ToRow).ToArray();
    if (replace)
    {
      Messages = rows;
    }
    else
    {
      var seenIds = Messages.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
      Messages = [.. rows.Where(row => seenIds.Add(row.Id)), .. Messages];
    }
    messageCursor = response.PageInfo.EndCursor;
    HasMoreMessages = response.PageInfo.HasNextPage || response.PageInfo.HasMore == true;
  }

}
