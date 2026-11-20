using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

/// <summary>Copy for community application, role, ownership and digest notifications.</summary>
internal static class NotificationCommunityText
{
  public static NotificationText ApplicationApproved(NotificationRenderInput input) =>
      WithCommunity(
          input,
          UiMessageKey.NotificationsKindCommunityApplicationApprovedTitle,
          UiMessageKey.NotificationsKindCommunityApplicationApprovedBody);

  public static NotificationText ApplicationRejected(NotificationRenderInput input)
  {
    var community = input.CommunityName();
    var reason = input.PublicText(input.Notification.CommunityApplicationId);
    return NotificationText.Of(
        UiText.Localized(UiMessageKey.NotificationsKindCommunityApplicationRejectedTitle, ("community", community)),
        reason.Length > 0
            ? UiText.Localized(
                UiMessageKey.NotificationsKindCommunityApplicationRejectedBodyWithReason,
                ("reason", UiText.UserContent(reason)))
            : UiText.Localized(
                UiMessageKey.NotificationsKindCommunityApplicationRejectedBody,
                ("community", community)));
  }

  public static NotificationText RoleChanged(NotificationRenderInput input) =>
      NotificationText.Of(
          UiText.Localized(
              UiMessageKey.NotificationsKindCommunityRoleChangedTitle,
              ("community", input.CommunityName())),
          UiText.Localized(
              UiMessageKey.NotificationsKindCommunityRoleChangedBody,
              ("from", Role(input.StringParam("from_role"))),
              ("to", Role(input.StringParam("to_role")))));

  public static NotificationText OwnershipReceived(NotificationRenderInput input) =>
      WithCommunity(
          input,
          UiMessageKey.NotificationsKindCommunityOwnershipReceivedTitle,
          UiMessageKey.NotificationsKindCommunityOwnershipReceivedBody);

  public static NotificationText OwnershipTransferredAway(NotificationRenderInput input) =>
      WithCommunity(
          input,
          UiMessageKey.NotificationsKindCommunityOwnershipTransferredAwayTitle,
          UiMessageKey.NotificationsKindCommunityOwnershipTransferredAwayBody);

  private static NotificationText WithCommunity(
      NotificationRenderInput input,
      UiMessageKey title,
      UiMessageKey body)
  {
    var community = ("community", (object?)input.CommunityName());
    return NotificationText.Of(UiText.Localized(title, community), UiText.Localized(body, community));
  }

  private static UiText Role(string? role) => UiText.Localized(role switch
  {
    "owner" => UiMessageKey.NotificationsKindRoleOwner,
    "moderator" => UiMessageKey.NotificationsKindRoleModerator,
    _ => UiMessageKey.NotificationsKindRoleMember,
  });
}
