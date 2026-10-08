using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private PendingLocalTurn? pendingLocalTurn;

  private sealed record PendingLocalTurn(
      string ConversationId,
      ChatProviderStatus Provider,
      CreateClientGeneratedChatBody Body);

  private PendingLocalTurn? RetryableTurn(string? conversationId, string text, ChatProviderStatus provider)
  {
    var pending = pendingLocalTurn;
    if (pending is not null &&
        pending.ConversationId == conversationId &&
        pending.Body.Message == text &&
        SameProviderIdentity(pending.Provider, provider))
    {
      return pending;
    }
    pendingLocalTurn = null;
    return null;
  }

  private static bool SameProviderIdentity(ChatProviderStatus left, ChatProviderStatus right) =>
      left.Kind == right.Kind &&
      left.ModelProvider == right.ModelProvider &&
      left.ModelName == right.ModelName;
}
