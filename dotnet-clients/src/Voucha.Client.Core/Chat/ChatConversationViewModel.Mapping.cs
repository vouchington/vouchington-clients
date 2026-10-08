using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private ChatMessageRow MapMessage(ChatMessage message)
  {
    var role = message.Content.Role;
    var content = message.Content.DisplayText;
    var completionStatus = message.Completion?.Status;
    var isIncomplete = string.Equals(completionStatus, "incomplete", StringComparison.Ordinal);
    var error = message.Content.Error;
    var errorMessageKey = isIncomplete && string.IsNullOrWhiteSpace(error)
        ? UiMessageKey.NativeDotnetChatConversationResponseInterrupted
        : (UiMessageKey?)null;
    return new ChatMessageRow(
        message.Id,
        role,
        content,
        message.CreatedAt,
        isIncomplete || !string.IsNullOrWhiteSpace(error),
        error,
        completionStatus,
        errorMessageKey,
        localization);
  }
}
