using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Friends;

public sealed record FriendRow(
    string Id,
    string? DisplayName,
    string? Title,
    string? Username,
    string? ProfileImageId,
    bool IsFollowing,
    bool IsSelf,
    IUiLocalization? Localization = null)
{
  public bool CanToggleFollow => !IsSelf;

  public string PrimaryLabel => NormalizeLabel(DisplayName) ?? NormalizeLabel(Title) ?? NormalizeLabel(Username) ?? NormalizeLabel(Id) ?? string.Empty;

  public string SecondaryLabel => UiUserHandle.FromUsername(Username).Value;

  public string AvatarInitial => string.IsNullOrEmpty(PrimaryLabel) ? string.Empty : PrimaryLabel[..1].ToUpperInvariant();

  public string FollowActionLabel => (Localization ?? UiLocalization.English).Localize(
      IsFollowing ? UiMessageKey.NativeDotnetDynamicUnfollow : UiMessageKey.NativeDotnetDynamicFollow);

  private static string? NormalizeLabel(string? label) => string.IsNullOrWhiteSpace(label) ? null : label.Trim();
}
