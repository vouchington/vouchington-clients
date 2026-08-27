using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Friends;

public sealed partial class FriendsViewModel
{
  private static string? DisplayNameFor(User user) =>
      DisplayAccountNameFor(user) ??
      ValueDisplayNameSource(user.DisplayNameSource) ??
      NonBlank(user.VerifiedDisplayName) ??
      NonBlank(user.Name);

  private static string? DisplayAccountNameFor(User user)
  {
    if (NonBlank(user.DisplayAccount?.Name) is { } displayName) return displayName;
    return user.UseDisplayNameFrom switch
    {
      "facebook" => NonBlank(user.FacebookAccount?.Name),
      "apple" => NonBlank(user.AppleAccount?.Name),
      "google" => NonBlank(user.GoogleAccount?.Name),
      "x" => NonBlank(user.XAccount?.Name),
      "linkedin" => NonBlank(user.LinkedinAccount?.Name),
      "microsoft" => NonBlank(user.MicrosoftAccount?.Name),
      "github" => NonBlank(user.GithubAccount?.Name),
      _ => null,
    };
  }

  private static string? ValueDisplayNameSource(string? source) =>
      source is "username" or "name" or "verified_display_name" or "facebook" or "apple" or "google" or "x" or
          "linkedin" or "microsoft" or "github"
          ? null
          : NonBlank(source);

  private static string? NonBlank(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value;
}
