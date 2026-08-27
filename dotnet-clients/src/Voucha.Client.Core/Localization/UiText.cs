namespace Voucha.Client.Core.Localization;

public readonly record struct UiText
{
  private UiText(
      UiMessageKey? key,
      string? verbatimValue,
      decimal? numberValue,
      IReadOnlyList<(string Name, object? Value)> arguments)
  {
    Key = key;
    VerbatimValue = verbatimValue;
    NumberValue = numberValue;
    Arguments = arguments;
  }

  public UiMessageKey? Key { get; }

  public string? VerbatimValue { get; }

  public decimal? NumberValue { get; }

  public IReadOnlyList<(string Name, object? Value)> Arguments { get; }

  public static UiText Localized(
      UiMessageKey key,
      params (string Name, object? Value)[] arguments) =>
      new(key, null, null, arguments);

  public static UiText Verbatim(string value) => new(null, value ?? string.Empty, null, []);

  public static UiText Number(decimal value) => new(null, null, value, []);

  public static UiText ProtocolValue(string? value) =>
      new(null, value ?? string.Empty, null, []);

  public static UiText UserContent(string? value) =>
      new(null, value ?? string.Empty, null, []);

  public static UiText ExternalContent(string? value) =>
      new(null, value ?? string.Empty, null, []);
}
