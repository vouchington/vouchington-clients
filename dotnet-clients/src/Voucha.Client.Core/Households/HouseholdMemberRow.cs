using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Households;

public sealed record HouseholdMemberRow(
    HouseholdMembership Membership,
    UiText DisplayNameText,
    string? ProtocolRelationship,
    bool CanRemove,
    IUiLocalization Localization)
{
  public string Id => Membership.Id;

  public string LocalizedDisplayName => Localization.Resolve(DisplayNameText);

  public static HouseholdMemberRow FromMembership(
      HouseholdMembership membership,
      bool canRemove,
      IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(membership);
    var username = membership.Individual.Username?.Trim();
    var relationship = membership.Relationship?.Trim();
    return new(
        membership,
        string.IsNullOrEmpty(username)
            ? UiText.Localized(UiMessageKey.NativeSwiftHouseholdsBookmarksHouseholdMember)
            : UiText.UserContent($"@{username}"),
        string.IsNullOrEmpty(relationship) ? null : relationship,
        canRemove,
        localization ?? UiLocalization.English);
  }
}
