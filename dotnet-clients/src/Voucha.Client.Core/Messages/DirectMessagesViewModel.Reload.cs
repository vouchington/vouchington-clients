using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Messages;

public sealed partial class DirectMessagesViewModel
{
  private string? conversationCursor;
  private bool hasMoreConversations = true;
  private bool conversationLoading;
  private bool pendingInboxReload;

  public Task LoadInboxAsync(CancellationToken cancellationToken = default) =>
      Conversations.Count == 0 ? ReloadInboxAsync(cancellationToken) : Task.CompletedTask;

  public async Task ReloadInboxAsync(CancellationToken cancellationToken = default)
  {
    await LoadConversationPageAsync(null, replaceRows: true, cancellationToken).ConfigureAwait(true);
  }

  public async Task LoadMoreConversationsAsync(CancellationToken cancellationToken = default)
  {
    await LoadConversationPageAsync(conversationCursor, replaceRows: false, cancellationToken).ConfigureAwait(true);
  }

  private async Task LoadConversationPageAsync(
      string? cursor,
      bool replaceRows,
      CancellationToken cancellationToken)
  {
    if (conversationLoading)
    {
      if (replaceRows)
      {
        pendingInboxReload = true;
      }

      return;
    }

    if (!replaceRows && !HasMoreConversations)
    {
      return;
    }

    conversationLoading = true;
    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      var response = await messagesService
          .FetchDirectMessagesAsync(new FetchDirectMessagesRequest(cursor), cancellationToken)
          .ConfigureAwait(true);
      var rows = response.Results.Select(conversation => ToRow(conversation)).ToArray();
      if (replaceRows)
      {
        Conversations = rows;
      }
      else
      {
        var seenIds = Conversations.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        Conversations = [.. Conversations, .. rows.Where(row => seenIds.Add(row.Id))];
      }
      conversationCursor = response.PageInfo.EndCursor;
      HasMoreConversations = response.PageInfo.HasNextPage || response.PageInfo.HasMore == true;
      State = LoadState.Loaded;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
    finally
    {
      conversationLoading = false;
    }

    if (pendingInboxReload)
    {
      pendingInboxReload = false;
      await ReloadInboxAsync(cancellationToken).ConfigureAwait(true);
    }
  }

  private async Task<bool> ReloadSelectedThreadAsync(CancellationToken cancellationToken = default)
  {
    if (SelectedConversationId is not { Length: > 0 } conversationId)
    {
      return false;
    }

    var loadGeneration = BeginSelectedConversationLoad();
    ThreadState = LoadState.Loading;
    ThreadErrorMessage = null;
    try
    {
      var conversationTask = messagesService.FetchDirectConversationAsync(conversationId, cancellationToken);
      var messagesTask = messagesService.FetchDirectConversationMessagesAsync(
          new FetchDirectConversationMessagesRequest(conversationId), cancellationToken);
      var participantsTask = messagesService.FetchDirectConversationParticipantsAsync(conversationId, cancellationToken);
      await Task.WhenAll(conversationTask, messagesTask, participantsTask).ConfigureAwait(true);

      if (!IsCurrentSelectedConversationLoad(conversationId, loadGeneration))
      {
        return false;
      }

      var conversation = await conversationTask.ConfigureAwait(true);
      var messagePage = await messagesTask.ConfigureAwait(true);
      var participantPage = await participantsTask.ConfigureAwait(true);
      if (!IsCurrentSelectedConversationLoad(conversationId, loadGeneration))
      {
        return false;
      }
      var rawParticipants = participantPage.Results;
      var isOwner = rawParticipants.Any(participant =>
          string.Equals(participant.UserId, currentUserId, StringComparison.Ordinal) &&
          string.Equals(participant.Role, "owner", StringComparison.Ordinal));
      ParticipantAddPolicy = conversation.Conversation.ParticipantAddPolicy ?? "owner_only";
      CompleteMessagePage(messagePage, replace: true);
      Participants = rawParticipants.Select(participant => ToRow(participant, isOwner)).ToArray();
      ThreadState = LoadState.Loaded;
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (!IsCurrentSelectedConversationLoad(conversationId, loadGeneration))
      {
        return false;
      }

      ThreadErrorMessage = ex.Message;
      ThreadState = LoadState.Error;
      return false;
    }
  }

  private async Task ApplyMutationAsync(string conversationId, Task mutationTask, Action apply)
  {
    try
    {
      await mutationTask.ConfigureAwait(true);
      if (!string.Equals(SelectedConversationId, conversationId, StringComparison.Ordinal))
      {
        return;
      }

      apply();
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (!string.Equals(SelectedConversationId, conversationId, StringComparison.Ordinal))
      {
        return;
      }

      ThreadErrorMessage = ex.Message;
      ThreadState = LoadState.Error;
    }
  }

  private async Task ApplyMutationAsync<T>(string conversationId, Task<T> mutationTask, Action<T> apply)
  {
    try
    {
      var response = await mutationTask.ConfigureAwait(true);
      if (!string.Equals(SelectedConversationId, conversationId, StringComparison.Ordinal))
      {
        return;
      }

      apply(response);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (!string.Equals(SelectedConversationId, conversationId, StringComparison.Ordinal))
      {
        return;
      }

      ThreadErrorMessage = ex.Message;
      ThreadState = LoadState.Error;
    }
  }
}
