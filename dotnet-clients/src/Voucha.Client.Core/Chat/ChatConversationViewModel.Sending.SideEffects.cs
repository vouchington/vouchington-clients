using System.Text;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private RetainedWindowsBuiltInToolResult? retainedWindowsToolResult;

  public RetainedWindowsBuiltInToolResult? RetainedWindowsToolResult => retainedWindowsToolResult;

  private void ReleaseBlockedRetry(string requestText)
  {
    blockedToolRetryText = null;
    if (retainedWindowsToolResult is { } retained && retained.RequestText != requestText)
      SetRetainedWindowsToolResult(null);
  }

  private void HandleKnownToolOutcome(
      Exception error, int currentRequest, string? conversationId, ILocalChatProvider provider,
      string assistantMessageId, bool assistantMessageInserted, string userMessageId,
      bool userMessageInserted, string text)
  {
    if (currentRequest != Volatile.Read(ref requestId)) return;
    pendingLocalTurn = null;
    if (assistantMessageInserted) RemoveLocalMessage(assistantMessageId);
    if (userMessageInserted) RemoveLocalMessage(userMessageId);
    blockedToolRetryText = text;
    SetRetainedWindowsToolResult(error is WindowsBuiltInResultRetainedException retained
        ? AcceptedRetainedResult(conversationId, text, provider, retained) : null);
    ErrorMessage = localization.Localize(UiMessageKey.SharedChatMcpPossibleSideEffect);
    State = LoadState.Error;
  }

  private void SetRetainedWindowsToolResult(RetainedWindowsBuiltInToolResult? retained)
  {
    if (retainedWindowsToolResult == retained) return;
    retainedWindowsToolResult = retained;
    OnPropertyChanged(nameof(RetainedWindowsToolResult));
  }

  private static RetainedWindowsBuiltInToolResult? AcceptedRetainedResult(
      string? conversationId, string requestText, ILocalChatProvider provider,
      WindowsBuiltInResultRetainedException retained)
  {
    if (conversationId is not { Length: > 0 } || provider is not IMcpLocalChatProvider mcp ||
        retained.AccountId.Length == 0) return null;
    var snapshot = mcp.ConnectedMcpSnapshot();
    if (snapshot is null || snapshot.AccountId != retained.AccountId ||
        snapshot.AccountEpoch != retained.AccountEpoch ||
        !mcp.IsCurrent(snapshot) || !mcp.MatchesAccountEpoch(snapshot) ||
        retained.CallId.Length is 0 or > 128 ||
        Encoding.UTF8.GetByteCount(retained.Envelope) is 0 or > MemberMcpToolBridge.MaximumEnvelopeBytes)
      return null;
    var post = OwnedPost(retained.Receipt, snapshot.AccountId, retained.CallId);
    var rss = OwnedRss(retained.RssItemReceipt, snapshot.AccountId, retained.CallId);
    if (retained.Receipt is not null && post is null ||
        retained.RssItemReceipt is not null && rss is null)
      return null;
    return new(conversationId, requestText, retained.AccountId, retained.AccountEpoch, retained.CallId,
        retained.Envelope, post, rss);
  }

  private static McpFeedPostReceipt? OwnedPost(McpFeedPostReceipt? receipt, string accountId, string callId)
  {
    if (receipt is null) return null;
    if (receipt.AccountId != accountId || receipt.CallId != callId || receipt.PostIds.Count is 0 or > 100 ||
        receipt.PostIds.Any(id => !Guid.TryParse(id, out _))) return null;
    return new(receipt.AccountId, receipt.CallId, receipt.PostIds.ToArray());
  }

  private static McpFeedRssItemReceipt? OwnedRss(McpFeedRssItemReceipt? receipt, string accountId, string callId)
  {
    if (receipt is null) return null;
    if (receipt.AccountId != accountId || receipt.CallId != callId || receipt.ItemIds.Count is 0 or > 100 ||
        receipt.ItemIds.Any(id => !Guid.TryParse(id, out _))) return null;
    return new(receipt.AccountId, receipt.CallId, receipt.ItemIds.ToArray());
  }
}
