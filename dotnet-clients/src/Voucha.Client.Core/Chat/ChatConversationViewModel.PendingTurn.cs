using Voucha.Client.Core.Api;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private PendingLocalTurn? pendingLocalTurn;

  private sealed record PendingLocalTurn(
      string ConversationId,
      ChatProviderStatus Provider,
      CreateClientGeneratedChatBody Body,
      MemberMcpConnectedSnapshot? McpSnapshot);

  private ChatProviderStatus ProviderForPendingMcpRetry(string text) =>
      pendingLocalTurn is { McpSnapshot: not null } pending &&
      pending.ConversationId == ConversationId && pending.Body.Message == text
          ? pending.Provider : SelectedProviderStatus;

  private bool HasConflictingCompletedMcpTurn(string text) =>
      pendingLocalTurn is { McpSnapshot: not null } pending &&
      pending.ConversationId == ConversationId &&
      !string.Equals(pending.Body.Message, text, StringComparison.Ordinal);

  private bool RejectInvalidatedCompletedMcpAccount(string text, ILocalChatProvider provider)
  {
    if (pendingLocalTurn is not { McpSnapshot: { } snapshot } pending ||
        pending.ConversationId != ConversationId || pending.Body.Message != text ||
        provider is IMcpLocalChatProvider mcp && mcp.MatchesAccount(snapshot.AccountId)) return false;
    ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetChatConversationLocalChatUnavailable);
    State = LoadState.Error;
    return true;
  }

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
    if (pending?.McpSnapshot is null) pendingLocalTurn = null;
    return null;
  }

  public void NotifyDraftEdited(string? text)
  {
    if (blockedToolRetryText is not null &&
        !string.Equals(text?.Trim(), blockedToolRetryText, StringComparison.Ordinal))
      blockedToolRetryText = null;
    if (pendingLocalTurn is { McpSnapshot: null } pending &&
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
