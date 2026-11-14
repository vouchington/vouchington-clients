using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Friends;

public sealed record FriendRow(
    string Id,
    string? DisplayName,
    string? Title,
    string? Username,
    string? ProfileImageId,
    bool IsFollowing,
    bool IsSelf,
    IUiLocalization? Localization = null,
    TopicImagePlacement? ProfileImagePlacement = null)
{
  public bool CanToggleFollow => !IsSelf;

  public string PrimaryLabel => TrimmedText.NonEmpty(DisplayName) ?? TrimmedText.NonEmpty(Title) ?? TrimmedText.NonEmpty(Username) ?? TrimmedText.NonEmpty(Id) ?? string.Empty;

  public string SecondaryLabel => UiUserHandle.FromUsername(Username).Value;

  public string AvatarInitial => string.IsNullOrEmpty(PrimaryLabel) ? string.Empty : PrimaryLabel[..1].ToUpperInvariant();

  public string FollowActionLabel => (Localization ?? UiLocalization.English).Localize(
      IsFollowing ? UiMessageKey.NativeDotnetDynamicUnfollow : UiMessageKey.NativeDotnetDynamicFollow);
}
