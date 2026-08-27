using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Messages;

public sealed partial class DirectMessagesViewModel
{
  private DirectMessageParticipantRow ToRow(
      DirectMessageParticipant participant,
      bool isOwner)
  {
    var canRemove = participant.UserId is not null &&
        ((isOwner && !string.Equals(
            participant.UserId,
            currentUserId,
            StringComparison.Ordinal)) ||
        (!isOwner && string.Equals(
            participant.UserId,
            currentUserId,
            StringComparison.Ordinal)));
    return new DirectMessageParticipantRow(
        participant.Id,
        participant.UserId,
        string.IsNullOrWhiteSpace(participant.Username)
            ? UiText.Localized(UiMessageKey.NativeDotnetDirectMessagesMember)
            : UiText.Verbatim(UiUserHandle.FromUsername(participant.Username).Value),
        participant.Role,
        participant.Role switch
        {
          "owner" => UiText.Localized(UiMessageKey.NativeDotnetDirectMessagesRoleOwner),
          "member" => UiText.Localized(UiMessageKey.NativeDotnetDirectMessagesRoleMember),
          _ => UiText.Verbatim(participant.Role),
        },
        canRemove,
        UiText.Localized(
            string.Equals(participant.UserId, currentUserId, StringComparison.Ordinal)
                ? UiMessageKey.NativeDotnetCsharpCommunitiesLeave
                : UiMessageKey.NativeDotnetCsharpCommunitiesRemove),
        localization);
  }
}
