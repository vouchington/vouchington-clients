using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Messages;

public sealed partial class DirectMessagesViewModel
{
  public async Task AddParticipantAsync(string userId, CancellationToken cancellationToken = default)
  {
    if (SelectedConversationId is not { Length: > 0 } conversationId || string.IsNullOrWhiteSpace(userId)) return;
    try
    {
      var response = await messagesService.AddDirectConversationParticipantAsync(
          new AddDirectConversationParticipantRequest(conversationId, userId),
          cancellationToken).ConfigureAwait(true);
      if (!string.Equals(SelectedConversationId, conversationId, StringComparison.Ordinal))
      {
        return;
      }

      Participants = [.. Participants, ToRow(response.Participant, IsOwner)];
      await ReloadConversationInboxRowAsync(conversationId, cancellationToken).ConfigureAwait(true);
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

  public async Task RemoveParticipantAsync(string userId, CancellationToken cancellationToken = default)
  {
    if (SelectedConversationId is not { Length: > 0 } conversationId || string.IsNullOrWhiteSpace(userId)) return;
    try
    {
      await messagesService.RemoveDirectConversationParticipantAsync(conversationId, userId, cancellationToken)
          .ConfigureAwait(true);
      if (!string.Equals(SelectedConversationId, conversationId, StringComparison.Ordinal))
      {
        return;
      }

      if (string.Equals(userId, currentUserId, StringComparison.Ordinal))
      {
        ClearSelectedConversationState();
        Conversations = Conversations.Where(row => row.Id != conversationId).ToArray();
        return;
      }

      Participants = Participants.Where(row => row.UserId != userId).ToArray();
      await ReloadConversationInboxRowAsync(conversationId, cancellationToken).ConfigureAwait(true);
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

  private async Task ReloadConversationInboxRowAsync(string conversationId, CancellationToken cancellationToken)
  {
    var conversationTask = messagesService.FetchDirectConversationAsync(conversationId, cancellationToken);
    var participantsTask = messagesService.FetchDirectConversationParticipantsAsync(conversationId, cancellationToken);
    await Task.WhenAll(conversationTask, participantsTask).ConfigureAwait(true);
    if (!string.Equals(SelectedConversationId, conversationId, StringComparison.Ordinal))
    {
      return;
    }

    var conversation = await conversationTask.ConfigureAwait(true);
    var participants = await participantsTask.ConfigureAwait(true);
    ParticipantAddPolicy = conversation.Conversation.ParticipantAddPolicy ?? "owner_only";
    UpsertConversationFallbackRow(ToRow(
        conversation.Conversation,
        participants.Results
            .Where(participant => !string.Equals(participant.UserId, currentUserId, StringComparison.Ordinal))
            .Select(participant => participant.Username)
            .OfType<string>()
            .Where(username => !string.IsNullOrWhiteSpace(username))
            .ToArray()));
  }

  private void UpsertConversationFallbackRow(DirectConversationRow row)
  {
    var existing = Conversations.FirstOrDefault(conversation =>
        string.Equals(conversation.Id, row.Id, StringComparison.Ordinal));
    if (existing is null && string.IsNullOrWhiteSpace(row.Title))
    {
      return;
    }

    var nextRow = existing is not null && string.IsNullOrWhiteSpace(row.Title)
        ? row with { Title = existing.Title }
        : row;
    Conversations = [nextRow, .. Conversations.Where(conversation =>
        !string.Equals(conversation.Id, row.Id, StringComparison.Ordinal))];
  }
}
