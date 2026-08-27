using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Messages;

public sealed partial class DirectMessagesViewModel
{
  private DirectMessageRow ToRow(DirectMessage message) =>
      ToRow(message, null);

  private DirectMessageRow ToRow(DirectMessage message, UiText? fallbackSenderText) =>
      new(
          message.Id,
          message.BodyText,
          string.IsNullOrWhiteSpace(message.SenderUsername)
              ? fallbackSenderText
                  ?? UiText.Localized(UiMessageKey.NativeDotnetDirectMessagesMessage)
              : UiText.Verbatim(UiUserHandle.FromUsername(message.SenderUsername).Value),
          message.CreatedAt,
          false,
          localization);
}
