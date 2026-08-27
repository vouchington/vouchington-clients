using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Friends;

public sealed record FriendRecommendationRow(
    FriendRecommendation Recommendation,
    User? User,
    IUiLocalization? Localization = null)
{
  public string Id => Recommendation.Id;

  public string PrimaryLabel =>
      UserDisplayName(User) ??
      NonBlank(Recommendation.ProviderFriendName) ??
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
    OAuthBrokerProvider.Facebook => UiMessageKey.NativeSwiftPresentationValuesFacebook,
    OAuthBrokerProvider.X => UiMessageKey.NativeSwiftPresentationValuesX,
    OAuthBrokerProvider.Github => UiMessageKey.NativeSwiftPresentationValuesGithub,
    _ => throw new ArgumentOutOfRangeException(nameof(Recommendation.Provider)),
  };

  private static string? UserDisplayName(User? user)
  {
    if (user is null) return null;
    return NonBlank(user.DisplayAccount?.Name) ??
        NonBlank(user.VerifiedDisplayName) ??
        NonBlank(user.Name) ??
        NonBlank(user.Username);
  }

  private static string? NonBlank(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  private static string LowercaseFirst(string value) =>
      string.IsNullOrEmpty(value)
          ? value
          : char.ToLowerInvariant(value[0]) + value[1..];
}
