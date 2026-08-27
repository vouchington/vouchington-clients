using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  public string ApiKeyLabel
  {
    get => apiKeyLabel;
    set => SetProperty(ref apiKeyLabel, value ?? string.Empty);
  }

  public string ApiKeyType
  {
    get => apiKeyType;
    set
    {
      if (SetProperty(ref apiKeyType, value ?? "rss"))
      {
        OnPropertyChanged(nameof(SelectedApiKeyTypeOption));
      }
    }
  }

  public IReadOnlyList<UiProtocolOption> ApiKeyTypeOptions =>
      LocalizedOptions(UiTaxonomy.ApiKeyTypes);

  public UiProtocolOption SelectedApiKeyTypeOption
  {
    get => SelectedOption(ApiKeyTypeOptions, ApiKeyType);
    set => ApiKeyType = value?.ProtocolValue ?? "rss";
  }

  public IReadOnlyList<UiProtocolOption> DisplayNameSourceOptions =>
      LocalizedOptions(UiTaxonomy.DisplayNameSources);

  public UiProtocolOption SelectedDisplayNameSourceOption
  {
    get => SelectedOption(DisplayNameSourceOptions, DisplayNameSource);
    set => DisplayNameSource = value?.ProtocolValue ?? "username";
  }

  public IReadOnlyList<UiProtocolOption> ProfileLinkTypeOptions =>
      LocalizedOptions(UiTaxonomy.ProfileLinkTypes);

  public UiProtocolOption SelectedProfileLinkTypeOption
  {
    get => SelectedOption(ProfileLinkTypeOptions, ProfileLinkType);
    set => ProfileLinkType = value?.ProtocolValue ?? "url";
  }

  private UiProtocolOption[] LocalizedOptions(
      IReadOnlyList<UiProtocolOptionDefinition> definitions) =>
      definitions.Select(definition => UiProtocolOption.From(definition, localization)).ToArray();

  private static UiProtocolOption SelectedOption(
      IReadOnlyList<UiProtocolOption> options,
      string protocolValue) =>
      options.First(option =>
          string.Equals(option.ProtocolValue, protocolValue, StringComparison.Ordinal));
}
