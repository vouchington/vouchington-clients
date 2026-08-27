using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Messages;

public sealed partial class DirectMessagesViewModel
{
  private long BeginSelectedConversationLoad() =>
      Interlocked.Increment(ref selectedConversationLoadGeneration);

  private bool IsCurrentSelectedConversationLoad(string conversationId, long generation) =>
      generation == selectedConversationLoadGeneration &&
      string.Equals(SelectedConversationId, conversationId, StringComparison.Ordinal);

  private bool IsCurrentOlderMessagesLoad(
      string conversationId,
      long generation,
      string? cursor) =>
      generation == selectedConversationLoadGeneration &&
      string.Equals(SelectedConversationId, conversationId, StringComparison.Ordinal) &&
      string.Equals(messageCursor, cursor, StringComparison.Ordinal);

  private bool HasOlderMessagesLoadInProgress(
      string conversationId,
      long generation,
      string? cursor) =>
      olderMessagesLoading &&
      generation == olderMessagesLoadingGeneration &&
      string.Equals(olderMessagesLoadingConversationId, conversationId, StringComparison.Ordinal) &&
      string.Equals(olderMessagesLoadingCursor, cursor, StringComparison.Ordinal);

  private void ClearOlderMessagesLoadingState()
  {
    olderMessagesLoading = false;
    olderMessagesLoadingConversationId = null;
    olderMessagesLoadingGeneration = 0;
    olderMessagesLoadingCursor = null;
  }

  private void ResetParticipantSearchState()
  {
    Interlocked.Increment(ref participantSearchGeneration);
    latestParticipantUserSearchQuery = string.Empty;
    ParticipantUserResults = [];
  }

  private static DirectConversationRow ToRow(
      DirectConversation conversation,
      IReadOnlyList<string>? fallbackParticipantUsernames = null)
  {
    var names = conversation.ParticipantUsernames?
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .ToArray();
    if (names is not { Length: > 0 } && fallbackParticipantUsernames is not null)
    {
      names = fallbackParticipantUsernames
          .Where(name => !string.IsNullOrWhiteSpace(name))
          .ToArray();
    }

    var title = names is { Length: > 0 }
        ? string.Join(", ", names)
        : conversation.Title;
    return new DirectConversationRow(conversation.Id, title, conversation.UpdatedAt);
  }
}
