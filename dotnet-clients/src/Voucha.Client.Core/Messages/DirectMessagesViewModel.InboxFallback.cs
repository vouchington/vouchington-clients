using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Messages;

public sealed partial class DirectMessagesViewModel
{
  private void BumpConversationInboxRowIfReloadFailed(string conversationId, DateTimeOffset updatedAt)
  {
    if (State == LoadState.Error)
    {
      BumpConversationInboxRow(conversationId, updatedAt);
    }
  }

  private void BumpConversationInboxRow(string conversationId, DateTimeOffset updatedAt)
  {
    var existing = Conversations.FirstOrDefault(conversation =>
        string.Equals(conversation.Id, conversationId, StringComparison.Ordinal));
    if (existing is null)
    {
      return;
    }

    UpsertConversationFallbackRow(existing with { UpdatedAt = updatedAt });
  }
}
