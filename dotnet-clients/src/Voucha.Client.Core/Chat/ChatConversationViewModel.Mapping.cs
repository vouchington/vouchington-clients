using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private static ChatMessageRow MapMessage(ChatMessage message)
  {
    var role = message.Content.Role;
    var content = message.Content.DisplayText;
    return new ChatMessageRow(
        message.Id,
        role,
        content,
        message.CreatedAt,
        !string.IsNullOrWhiteSpace(message.Content.Error),
        message.Content.Error);
  }
}
