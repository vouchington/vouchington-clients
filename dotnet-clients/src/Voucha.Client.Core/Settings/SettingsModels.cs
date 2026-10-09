using System.Linq;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsSelectionRowViewModel : ObservableObject
{
  private readonly IUiLocalization localization;
  private SettingsOptionViewModel selectedOption;

  public SettingsSelectionRowViewModel(
      string key,
      UiText label,
      IReadOnlyList<SettingsOptionDefinition> options,
      string value,
      IUiLocalization localization)
  {
    Key = key;
    LabelText = label;
    this.localization = localization;
    Options = options
        .Select(option => new SettingsOptionViewModel(
            option.Value,
            option.LabelText,
            localization))
        .ToArray();
    selectedOption = Options.First(option =>
        string.Equals(option.Value, value, StringComparison.Ordinal));
  }

  public string Key { get; }

  public UiText LabelText { get; }

  public string Label => localization.Resolve(LabelText);

  public IReadOnlyList<SettingsOptionViewModel> Options { get; }

  public string Value
  {
    get => SelectedOption.Value;
    set => SelectedOption = Options.First(option =>
        string.Equals(option.Value, value, StringComparison.Ordinal));
  }

  public SettingsOptionViewModel SelectedOption
  {
    get => selectedOption;
    set => SetProperty(
        ref selectedOption,
        value ?? throw new ArgumentNullException(nameof(value)));
  }
}

public sealed record SettingsOptionViewModel(
    string Value,
    UiText LabelText,
    IUiLocalization Localization)
{
  public string DisplayLabel => Localization.Resolve(LabelText);
}

public readonly record struct SettingsOptionDefinition(string Value, UiText LabelText);

public sealed record SettingsProfileLinkRow(
    ProfileLink ProtocolValue,
    UiText LinkTypeText,
    IUiLocalization Localization)
{
  public string? UserContentName => ProtocolValue.Name;

  public string? UserContentAddress => ProtocolValue.Url;

  public string? UserContentHandle => ProtocolValue.Handle;

  public string LocalizedLinkType => Localization.Resolve(LinkTypeText);
}

public sealed partial class SettingsToggleRowViewModel : ObservableObject
{
  private readonly IUiLocalization localization;
  private bool isChecked;

  public SettingsToggleRowViewModel(
      string key,
      UiText label,
      UiText description,
      bool isChecked,
      IUiLocalization localization)
  {
    Key = key;
    LabelText = label;
    DescriptionText = description;
    this.isChecked = isChecked;
    this.localization = localization;
  }

  public string Key { get; }

  public UiText LabelText { get; }

  public UiText DescriptionText { get; }

  public string Label => localization.Resolve(LabelText);

  public string Description => localization.Resolve(DescriptionText);

  public bool IsChecked
  {
    get => isChecked;
    set => SetProperty(ref isChecked, value);
  }
}

public sealed record MembershipPlanOptionViewModel(
    string Slug,
    UiText NameText,
    IReadOnlyList<MembershipPlanPriceOptionViewModel> PriceOptions,
    UiText StateText,
    UiText PurchaseButtonText,
    UiText PurchaseExplanationText,
    bool IsCurrentPlan,
    IReadOnlyList<MembershipBenefitBullet> FeatureBulletModels,
    IUiLocalization Localization)
{
  public string Name => Localization.Resolve(NameText);

  public string StateLabel => Localization.Resolve(StateText);

  public string PurchaseButtonLabel => Localization.Resolve(PurchaseButtonText);

  public string PurchaseExplanation => Localization.Resolve(PurchaseExplanationText);

  public IReadOnlyList<string> FeatureBullets => FeatureBulletModels.Select(feature => feature.Text).ToArray();

  public string PriceOptionsText { get; init; } = PriceOptions.Count == 0
      ? Localization.Localize(UiMessageKey.NativeDotnetSettingsNoBillingData)
      : string.Join(Environment.NewLine, PriceOptions.Select(option => option.Label));

  public string FeatureBulletsText =>
      string.Join(Environment.NewLine, FeatureBullets.Select(feature => $"- {feature}"));
}

public sealed record MembershipBenefitBullet(
    UiText LabelText,
    UiText ValueText,
    IUiLocalization Localization)
{
  public string Text => string.Concat(
      Localization.Resolve(LabelText),
      ": ",
      Localization.Resolve(ValueText));
}

public sealed record MembershipPlanPriceOptionViewModel(
    string Id,
    UiText LabelText,
    UiText IntervalText,
    IUiLocalization Localization)
{
  public string Label => Localization.Resolve(LabelText);

  public string Interval => Localization.Resolve(IntervalText);
}

internal sealed record MembershipPlanDefinition(
    string Slug,
    UiMessageKey NameKey);

internal static class MembershipPlanCatalog
{
  public static readonly IReadOnlyList<MembershipPlanDefinition> Definitions =
  [
    new("free", UiMessageKey.NativeDotnetSettingsPlanFree),
    new("plus", UiMessageKey.NativeDotnetSettingsPlanPlus),
    new("pro", UiMessageKey.NativeDotnetSettingsPlanPro),
  ];
}
