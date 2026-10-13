using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private void HandlePossibleToolSideEffect(
      int currentRequest, string assistantMessageId, bool assistantMessageInserted,
      string userMessageId, bool userMessageInserted, string text)
  {
    if (currentRequest != Volatile.Read(ref requestId)) return;
    pendingLocalTurn = null;
    if (assistantMessageInserted) RemoveLocalMessage(assistantMessageId);
    if (userMessageInserted) RemoveLocalMessage(userMessageId);
    blockedToolRetryText = text;
    ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetChatConversationLocalChatUnavailable);
    State = LoadState.Error;
  }
}
