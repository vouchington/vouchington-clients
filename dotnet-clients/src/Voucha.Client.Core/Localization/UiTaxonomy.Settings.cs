namespace Voucha.Client.Core.Localization;

public static partial class UiTaxonomy
{
  public static readonly IReadOnlyList<UiProtocolOptionDefinition> DisplayNameSources =
  [
    O("username", UiMessageKey.NativeTaxonomySettingsUsername),
    O("facebook", UiMessageKey.NativeTaxonomySettingsFacebook),
    O("x", UiMessageKey.NativeTaxonomySettingsX),
    O("apple", UiMessageKey.NativeTaxonomySettingsApple),
    O("google", UiMessageKey.NativeTaxonomySettingsGoogle),
    O("linkedin", UiMessageKey.NativeTaxonomySettingsLinkedin),
    O("microsoft", UiMessageKey.NativeTaxonomySettingsMicrosoft),
    O("github", UiMessageKey.NativeTaxonomySettingsGithub),
  ];

  public static readonly IReadOnlyList<UiProtocolOptionDefinition> ProfileLinkTypes =
  [
    O("url", UiMessageKey.NativeTaxonomySettingsUrl),
    O("twitter", UiMessageKey.NativeTaxonomySettingsTwitter),
    O("facebook", UiMessageKey.NativeTaxonomySettingsFacebook),
    O("instagram", UiMessageKey.NativeTaxonomySettingsInstagram),
    O("github", UiMessageKey.NativeTaxonomySettingsGithub),
    O("linkedin", UiMessageKey.NativeTaxonomySettingsLinkedin),
    O("youtube", UiMessageKey.NativeTaxonomySettingsYoutube),
    O("tiktok", UiMessageKey.NativeTaxonomySettingsTiktok),
  ];

  public static readonly IReadOnlyList<UiProtocolOptionDefinition> ApiKeyTypes =
  [
    O("rss", UiMessageKey.NativeTaxonomySettingsRss),
    O("mcp", UiMessageKey.NativeTaxonomySettingsMcp),
  ];

  public static UiText ProfileLinkType(string? value) =>
      OptionText(ProfileLinkTypes, value);

  public static UiText ApiKeyType(string? value) =>
      OptionText(ApiKeyTypes, value);

  private static UiProtocolOptionDefinition O(string value, UiMessageKey key) =>
      new(value, key);

  private static UiText OptionText(
      IReadOnlyList<UiProtocolOptionDefinition> options,
      string? value)
  {
    var match = options.FirstOrDefault(option =>
        string.Equals(option.ProtocolValue, value, StringComparison.Ordinal));
    return string.IsNullOrEmpty(match.LabelKey.Value)
        ? UiText.ProtocolValue(value)
        : UiText.Localized(match.LabelKey);
  }
}
