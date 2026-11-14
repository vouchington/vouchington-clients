using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Friends;

public sealed record FriendRecommendationRow(
    FriendRecommendation Recommendation,
    User? User,
    IUiLocalization? Localization = null)
{
  public string Id => Recommendation.Id;

  public TopicImagePlacement? ProfileImagePlacement => User?.ProfileImagePlacement;

  public string PrimaryLabel =>
      UserDisplayName(User) ??
      TrimmedText.NonEmpty(Recommendation.ProviderFriendName) ??
      $"{UiInvariantText.VouchaBrand.Value} {LowercaseFirst((Localization ?? UiLocalization.English).Localize(UiMessageKey.NativeSwiftPresentationValuesMember))}";

  public string SecondaryLabel => UiUserHandle.FromUsername(User?.Username).Value;

  public string ProviderPresentation
  {
    get
    {
      var localization = Localization ?? UiLocalization.English;
      return localization.Format(
          UiMessageKey.ExtractedMyFriendRecommendationsListViaProviderlabel3c6f79cf,
          ("providerLabel", localization.Localize(ProviderMessageKey())));
    }
  }

  public string AvatarInitial =>
      string.IsNullOrEmpty(PrimaryLabel) ? string.Empty : PrimaryLabel[..1].ToUpperInvariant();

  private UiMessageKey ProviderMessageKey() => Recommendation.Provider switch
  {
    FriendRecommendationProvider.Facebook => UiMessageKey.NativeSwiftPresentationValuesFacebook,
    FriendRecommendationProvider.X => UiMessageKey.NativeSwiftPresentationValuesX,
    FriendRecommendationProvider.Github => UiMessageKey.NativeSwiftPresentationValuesGithub,
    _ => throw new ArgumentOutOfRangeException(nameof(Recommendation.Provider)),
  };

  private static string? UserDisplayName(User? user)
  {
    if (user is null) return null;
    return TrimmedText.NonEmpty(user.DisplayAccount?.Name) ??
        TrimmedText.NonEmpty(user.VerifiedDisplayName) ??
        TrimmedText.NonEmpty(user.Name) ??
        TrimmedText.NonEmpty(user.Username);
  }

  private static string LowercaseFirst(string value) =>
      string.IsNullOrEmpty(value)
          ? value
          : char.ToLowerInvariant(value[0]) + value[1..];
}
