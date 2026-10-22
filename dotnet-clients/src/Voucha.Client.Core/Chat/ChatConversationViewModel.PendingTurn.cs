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
      MemberMcpConnectedSnapshot? McpSnapshot,
      IMcpLocalChatProvider? McpProvider,
      IReadOnlyList<McpFeedPostReceipt>? FeedPostReceipts,
      IReadOnlyList<McpFeedRssItemReceipt>? RssItemReceipts);

  private ChatProviderStatus ProviderForPendingMcpRetry(string text) =>
      pendingLocalTurn is { McpSnapshot: not null } pending &&
      pending.ConversationId == ConversationId && pending.Body.Message == text
          ? pending.Provider : SelectedProviderStatus;

  private ILocalChatProvider? LocalProviderForSend(string text, ChatProviderStatus selected) =>
      pendingLocalTurn is { McpSnapshot: not null, McpProvider: { } savedProvider } pending &&
      pending.ConversationId == ConversationId && pending.Body.Message == text
          ? savedProvider
          : providerResolver is ILocalChatProviderResolver resolver
              ? resolver.GetLocalProvider(selected.ModelProvider ?? string.Empty)
              : localChatProvider;

  private bool HasConflictingCompletedMcpTurn(string text) =>
      pendingLocalTurn is { McpSnapshot: not null } pending &&
      pending.ConversationId == ConversationId &&
      !string.Equals(pending.Body.Message, text, StringComparison.Ordinal);

  private bool RejectConflictingCompletedMcpTurn(string text)
  {
    if (!HasConflictingCompletedMcpTurn(text)) return false;
    ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetChatMcpPendingTurnRetryRequired);
    State = LoadState.Error;
    return true;
  }

  private bool RejectInvalidatedCompletedMcpAccount(string text, ILocalChatProvider provider)
  {
    if (pendingLocalTurn is not { McpSnapshot: { } snapshot } pending ||
        pending.ConversationId != ConversationId || pending.Body.Message != text ||
        provider is IMcpLocalChatProvider mcp && mcp.MatchesAccountEpoch(snapshot)) return false;
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
        (pending.McpSnapshot is not null
            ? pending.Provider.Kind == provider.Kind &&
              pending.Provider.ModelProvider == provider.ModelProvider
            : SameProviderIdentity(pending.Provider, provider)) &&
        (pending.McpSnapshot is null || localProvider is IMcpLocalChatProvider mcp &&
            mcp.MatchesAccountEpoch(pending.McpSnapshot)))
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
    if (retainedWindowsToolResult is { } retained &&
        !string.Equals(text?.Trim(), retained.RequestText, StringComparison.Ordinal))
      SetRetainedWindowsToolResult(null);
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
