namespace Voucha.Client.Core.Localization;

public sealed class UiLocalizedValueFormatter
{
  private readonly IUiLocalization localization;

  public UiLocalizedValueFormatter(IUiLocalization localization) =>
      this.localization = localization ?? throw new ArgumentNullException(nameof(localization));

  public string Format(object? value, string? mode)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(mode);
    if (mode.StartsWith("message:", StringComparison.Ordinal))
    {
      var parts = mode["message:".Length..].Split('|', 2);
      var rendered = parts.Length == 2 ? Render(value, parts[1]) : value?.ToString() ?? string.Empty;
      return localization.Format(new UiMessageKey(parts[0]), ("value", rendered));
    }

    return mode switch
    {
      "dateTime" => FormatDateTime(value),
      "number" => FormatNumber(value),
      "percent" => localization.FormatPercent(ToDecimal(value)),
      "signedNumber" => FormatSignedNumber(value),
      _ => throw new ArgumentException($"Unknown localized value format mode '{mode}'.", nameof(mode)),
    };
  }

  private string Render(object? value, string mode) => mode switch
  {
    "number" => FormatNumber(value),
    "percent" => localization.FormatPercent(ToDecimal(value)),
    _ => throw new ArgumentException($"Unknown localized message value mode '{mode}'.", nameof(mode)),
  };

  private string FormatDateTime(object? value)
  {
    var instant = value switch
    {
      DateTimeOffset dateTimeOffset => dateTimeOffset,
      DateTime dateTime => new DateTimeOffset(dateTime),
      _ => throw new ArgumentException("Date formatting requires a DateTime or DateTimeOffset.", nameof(value)),
    };
    return localization.FormatDateTime(instant, TimeZoneInfo.Local);
  }

  private string FormatNumber(object? value) => localization.FormatNumber(ToDecimal(value));

  private string FormatSignedNumber(object? value)
  {
    var number = ToDecimal(value);
    var prefix = number > 0 ? "+" : string.Empty;
    return $"{prefix}{localization.FormatNumber(number)}";
  }

  private static decimal ToDecimal(object? value) =>
      Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
}
