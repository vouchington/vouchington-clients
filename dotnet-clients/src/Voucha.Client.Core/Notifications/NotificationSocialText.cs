using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Notifications;

/// <summary>Copy for follow, referral, message and household notifications.</summary>
internal static class NotificationSocialText
{
  public static NotificationText Follow(NotificationRenderInput input) =>
      NotificationText.TitleOnly(Actor(
          input,
          UiMessageKey.NotificationsKindFollowByUser,
          UiMessageKey.NotificationsKindFollowAnonymous));

  public static NotificationText FollowReferralSignup(NotificationRenderInput input) =>
      NotificationText.TitleOnly(Actor(
          input,
          UiMessageKey.NotificationsKindFollowReferralSignupByUser,
          UiMessageKey.NotificationsKindFollowReferralSignupAnonymous));

  public static NotificationText ReferralSignup(NotificationRenderInput input)
  {
    var count = input.NumberParam("referral_count");
    var body = UiText.Localized(
        count == 1
            ? UiMessageKey.NotificationsKindReferralSignupBodyOne
            : UiMessageKey.NotificationsKindReferralSignupBodyOther,
        ("count", UiText.Number(count)));
    return NotificationText.Of(
        Actor(
            input,
            UiMessageKey.NotificationsKindReferralSignupByUser,
            UiMessageKey.NotificationsKindReferralSignupAnonymous),
        body);
  }

  public static NotificationText ReferralClick(NotificationRenderInput input) =>
      NotificationText.TitleOnly(UiText.Localized(UiMessageKey.NotificationsKindReferralClickTitle));

  public static NotificationText DirectMessage(NotificationRenderInput input) =>
      NotificationText.TitleOnly(UiText.Localized(UiMessageKey.NotificationsKindDirectMessageTitle));

  public static NotificationText Modmail(NotificationRenderInput input) =>
      NotificationText.TitleOnly(UiText.Localized(UiMessageKey.NotificationsKindModmailTitle));

  public static NotificationText HouseholdInvitation(NotificationRenderInput input) =>
      NotificationText.Of(
          UiText.Localized(UiMessageKey.NotificationsKindHouseholdInvitationTitle),
          UiText.Localized(UiMessageKey.NotificationsKindHouseholdInvitationBody));

  private static UiText Actor(NotificationRenderInput input, UiMessageKey byUser, UiMessageKey anonymous) =>
      NotificationPostText.Named(input.Username(input.Notification.ActorUserId), byUser, anonymous);
}
