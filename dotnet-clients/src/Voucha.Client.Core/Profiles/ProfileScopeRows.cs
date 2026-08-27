using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Profiles;

public sealed record ProfileScopeTabRow(
    NativeUserProfileSection Section,
    NativeUserProfileCollectionKind Collection,
    UiText LabelText,
    int Count,
    bool IsSelected,
    bool IsVisible,
    IUiLocalization? Localization = null)
{
  private IUiLocalization UiLocalization =>
      Localization ?? Voucha.Client.Core.Localization.UiLocalization.English;

  public string Label => UiLocalization.Resolve(LabelText);

  public UiText? SelectionText => IsSelected
      ? UiText.Localized(UiMessageKey.NativeDotnetProfileSelected)
      : null;

  public string? SelectionLabel => SelectionText is UiText text
      ? UiLocalization.Resolve(text)
      : null;

  public UiText AccessibilityDescriptionText => IsSelected
      ? UiText.Localized(
          UiMessageKey.NativeDotnetProfileSelectedAccessibility,
          ("label", LabelText))
      : LabelText;

  public string AccessibilityDescription =>
      UiLocalization.Resolve(AccessibilityDescriptionText);
}
