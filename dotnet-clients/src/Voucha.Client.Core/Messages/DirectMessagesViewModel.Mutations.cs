using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Messages;

public sealed partial class DirectMessagesViewModel
{
  // Caps fan-out from a single search while still following page_info instead of silently
  // truncating at page one.
  private const int UserSearchPageLimit = 10;
  private const int UserSearchMaxAdditionalPages = 4;

  public async Task SearchUsersAsync(string query, CancellationToken cancellationToken = default)
  {
    await SearchUsersAsync(
        query,
        value => latestUserSearchQuery = value,
        () => latestUserSearchQuery,
        rows => ComposerUserResults = rows,
        cancellationToken)
        .ConfigureAwait(true);
  }

  // Shared by the composer search above and participant search in
  // DirectMessagesViewModel.ParticipantSearch.cs, each supplying its own staleness check via
  // getLatestQuery/isSuperseded.
  private async Task SearchUsersAsync(
      string query,
      Action<string> setLatestQuery,
      Func<string> getLatestQuery,
      Action<IReadOnlyList<DirectMessageUserRow>> setResults,
      CancellationToken cancellationToken,
      Func<bool>? isSuperseded = null)
  {
    var trimmed = query?.Trim() ?? string.Empty;
    setLatestQuery(trimmed);
    if (trimmed.Length == 0)
    {
      setResults([]);
      return;
    }

    bool IsCurrent() =>
        string.Equals(getLatestQuery(), trimmed, StringComparison.Ordinal) &&
        !(isSuperseded?.Invoke() ?? false);

    try
    {
      var matches = new List<DirectMessageUserRow>();
      string? after = null;
      for (var page = 0; page <= UserSearchMaxAdditionalPages; page++)
      {
        if (!IsCurrent()) return;
        var response = await messagesService
            .SearchUsersAsync(new SearchUsersRequest(trimmed, after, UserSearchPageLimit), cancellationToken)
            .ConfigureAwait(true);
        matches.AddRange(response.Results
            .Select(user => new DirectMessageUserRow(user.Id, user.Username, localization)));
        if (matches.Count >= UserSearchPageLimit || !response.PageInfo.HasNextPage || response.PageInfo.EndCursor is not { } endCursor)
        {
          break;
        }

        after = endCursor;
      }

      if (IsCurrent()) setResults(matches);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (!IsCurrent())
      {
        return;
      }

      setResults([]);
      ErrorMessage = ex.Message;
    }
  }

  public async Task<bool> CreateConversationAsync(
      IReadOnlyList<string>? userIds,
      string text,
      IReadOnlyList<string>? recipientUsernames = null,
      CancellationToken cancellationToken = default)
  {
    var trimmed = text?.Trim() ?? string.Empty;
    if (userIds is null || userIds.Count == 0 || trimmed.Length == 0 || createConversationLoading) return false;
    createConversationLoading = true;
    State = LoadState.Loading;
    ErrorMessage = null;
    DirectConversationResponse? createdConversation = null;
    try
    {
      createdConversation = await messagesService
          .CreateDirectConversationAsync(new CreateDirectConversationRequest(userIds), cancellationToken)
          .ConfigureAwait(true);
      var createdConversationId = createdConversation.Conversation.Id;
      SelectedConversationId = createdConversationId;
      ResetParticipantSearchState();
      messageCursor = null;
      HasMoreMessages = true;
      Messages = [];
      Participants = createdConversation.Conversation.Participants?.Select(participant => ToRow(participant, false)).ToArray() ?? [];
      var message = await messagesService
          .SendDirectMessageAsync(new SendDirectMessageRequest(createdConversation.Conversation.Id, trimmed), cancellationToken)
          .ConfigureAwait(true);
      var isStillSelected = string.Equals(SelectedConversationId, createdConversationId, StringComparison.Ordinal);
      if (isStillSelected)
      {
        Messages = [ToRow(
            message.Message,
            UiText.Localized(UiMessageKey.NativeDotnetDirectMessagesYou))];
        Participants = createdConversation.Conversation.Participants?.Select(participant => ToRow(participant, false)).ToArray() ?? [];
      }

      await ReloadInboxAsync(cancellationToken).ConfigureAwait(true);
      var conversationRow = ToRow(createdConversation.Conversation, recipientUsernames) with
      {
        UpdatedAt = message.Message.UpdatedAt ?? message.Message.CreatedAt,
      };
      if (State == LoadState.Error ||
          !Conversations.Any(row => string.Equals(row.Id, createdConversationId, StringComparison.Ordinal)))
      {
        UpsertConversationFallbackRow(conversationRow);
      }

      if (string.Equals(SelectedConversationId, createdConversationId, StringComparison.Ordinal))
      {
        await ReloadSelectedThreadAsync(cancellationToken).ConfigureAwait(true);
      }

      State = LoadState.Loaded;
      return true;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (createdConversation is not null)
      {
        UpsertConversationFallbackRow(ToRow(createdConversation.Conversation, recipientUsernames));
      }

      ErrorMessage = ex.Message;
      State = LoadState.Error;
      return false;
    }
    finally
    {
      createConversationLoading = false;
    }
  }

  public async Task UpdateParticipantAddPolicyAsync(
      string policy,
      CancellationToken cancellationToken = default)
  {
    if (SelectedConversationId is not { Length: > 0 } conversationId || string.IsNullOrWhiteSpace(policy)) return;
    await ApplyMutationAsync(
        conversationId,
        messagesService.UpdateDirectConversationParticipantPolicyAsync(
            new UpdateDirectConversationParticipantPolicyRequest(conversationId, policy),
            cancellationToken),
        response => ParticipantAddPolicy = response.ParticipantAddPolicy)
        .ConfigureAwait(true);
  }

  public void ClearSelectedConversationState()
  {
    SelectedConversationId = null;
    ResetParticipantSearchState();
    ClearOlderMessagesLoadingState();
    Messages = [];
    Participants = [];
    messageCursor = null;
    HasMoreMessages = true;
    ParticipantAddPolicy = "owner_only";
    ThreadErrorMessage = null;
    ThreadState = LoadState.Idle;
  }

}
