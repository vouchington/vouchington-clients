using Voucha.Client.Core.Api;
using Voucha.Client.Core.Agent;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private PendingLocalTurn? pendingLocalTurn;

  private sealed record PendingLocalTurn(
      string ConversationId,
      ChatProviderStatus Provider,
      CreateClientGeneratedChatBody Body,
      MemberMcpConnectedSnapshot? McpSnapshot);

  private PendingLocalTurn? RetryableTurn(
      string? conversationId, string text, ChatProviderStatus provider, ILocalChatProvider localProvider)
  {
    var pending = pendingLocalTurn;
    if (pending is not null &&
        pending.ConversationId == conversationId &&
        pending.Body.Message == text &&
        SameProviderIdentity(pending.Provider, provider) &&
        (pending.McpSnapshot is null || localProvider is IMcpLocalChatProvider mcp &&
            mcp.MatchesAccount(pending.McpSnapshot.AccountId)))
    {
      return pending;
    }
    pendingLocalTurn = null;
    return null;
  }

  public void NotifyDraftEdited(string? text)
  {
    if (blockedToolRetryText is not null &&
        !string.Equals(text?.Trim(), blockedToolRetryText, StringComparison.Ordinal))
      blockedToolRetryText = null;
    if (pendingLocalTurn is { } pending &&
        !string.Equals(text?.Trim(), pending.Body.Message, StringComparison.Ordinal))
    {
      pendingLocalTurn = null;
    }
  }

  private static bool SameProviderIdentity(ChatProviderStatus left, ChatProviderStatus right) =>
      left.Kind == right.Kind &&
      left.ModelProvider == right.ModelProvider &&
      (left.ModelName == right.ModelName ||
       right.ModelName is null && right.ModelProvider == LocalChatProviderIds.WindowsSystemLanguageModel);
}
