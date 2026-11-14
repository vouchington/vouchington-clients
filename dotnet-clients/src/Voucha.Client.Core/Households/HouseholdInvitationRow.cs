using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Households;

public sealed record HouseholdInvitationRow(HouseholdInvitation Invitation, bool IsSent, IUiLocalization Localization)
{
  public bool IsReceived => !IsSent;
  public string LocalizedRelationship => Localization.Resolve(UiText.UserContent(Invitation.Relationship ?? string.Empty));
  public UiText TitleText => IsSent
      ? UiText.Localized(UiMessageKey.SettingsHouseholdInvitationsSentFor, ("invitee", Name(Invitation.InvitedUser)))
      : Invitation.InvitedBy.Id == Invitation.HouseholdOwner.Id
          ? UiText.Localized(UiMessageKey.SettingsHouseholdInvitationsReceivedFromOwner, ("owner", Name(Invitation.HouseholdOwner)))
          : UiText.Localized(UiMessageKey.SettingsHouseholdInvitationsReceivedFromOther,
              ("inviter", Name(Invitation.InvitedBy)), ("owner", Name(Invitation.HouseholdOwner)));
  public string LocalizedTitle => Localization.Resolve(TitleText);

  private static UiText Name(HouseholdInvitationUser user) => string.IsNullOrWhiteSpace(user.Username)
      ? UiText.Localized(UiMessageKey.SettingsHouseholdInvitationsMemberFallback)
      : UiText.UserContent($"@{user.Username.Trim()}");
}
