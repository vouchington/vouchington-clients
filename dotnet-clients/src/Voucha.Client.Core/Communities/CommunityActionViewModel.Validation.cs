using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityActionViewModel
{
  public CommunityActionValidation Validation
  {
    get
    {
      if (kind != CommunityActionKind.Create)
      {
        return new CommunityActionValidation(true);
      }

      var trimmedName = (Name ?? string.Empty).Trim();
      if (trimmedName.Length is < 1 or > 100)
      {
        return Invalid(UiMessageKey.NativeDotnetCommunityCommunityNameLength);
      }

      if (trimmedName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 3)
      {
        return Invalid(UiMessageKey.NativeDotnetCommunityCommunityNameWords);
      }

      if (!string.IsNullOrWhiteSpace(Slug) &&
          (Slug.Length > 80 || Slug.Any(character => !IsSlugCharacter(character))))
      {
        return Invalid(UiMessageKey.NativeDotnetCommunityCommunitySlugInvalid);
      }

      return string.IsNullOrWhiteSpace(TurnstileToken) && !CanUseCaptchaBypass
          ? new CommunityActionValidation(
              false,
              localization.Localize(UiMessageKey.NativeDotnetValidationCaptchaRequired),
              RequiresCaptcha: true)
          : new CommunityActionValidation(true);
    }
  }

  private CommunityActionValidation Invalid(UiMessageKey key) =>
      new(false, localization.Localize(key));

  private static bool IsSlugCharacter(char character) =>
      character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
}
