namespace Voucha.Client.Core.Localization;

public readonly record struct UiProtocolOptionDefinition(
    string ProtocolValue,
    UiMessageKey LabelKey);

public sealed record UiProtocolOption(
    string ProtocolValue,
    UiText LabelText,
    IUiLocalization Localization)
{
  public string DisplayLabel => Localization.Resolve(LabelText);

  public static UiProtocolOption From(
      UiProtocolOptionDefinition definition,
      IUiLocalization localization) =>
      new(
          definition.ProtocolValue,
          UiText.Localized(definition.LabelKey),
          localization);
}
