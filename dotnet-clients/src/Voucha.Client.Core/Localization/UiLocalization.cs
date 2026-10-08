using System.Globalization;
using System.Numerics;
using System.Resources;
using System.Text.RegularExpressions;

namespace Voucha.Client.Core.Localization;

public interface IUiLocalization
{
  CultureInfo Culture { get; }

  string Localize(UiMessageKey key);

  string Format(UiMessageKey key, params (string Name, object? Value)[] arguments);

  string Resolve(UiText text);

  string FormatDateTime(DateTimeOffset instant, TimeZoneInfo timeZone);

  string FormatNumber(decimal value);

  string FormatPercent(decimal value);

  string FormatCurrency(decimal value, string currencyCode);

  string FormatCurrency(decimal value, string currencyCode, int maximumFractionDigits);

  string FormatCurrency(BigInteger scaledAmount, string currencyCode, int scale);

  string FormatCurrency(
      decimal value,
      string currencyCode,
      int minimumFractionDigits,
      int maximumFractionDigits);
}

public sealed partial class UiLocalization : IUiLocalization
{
  public static IUiLocalization English { get; } = new UiLocalization(
      new UiLocaleController(new EnglishLanguageProvider()));

  private static readonly ResourceManager Resources = new(
      "Voucha.Client.Core.Localization.Generated.UiMessages",
      typeof(UiLocalization).Assembly);
  private readonly IUiLocaleController localeController;

  public UiLocalization(
      IUiLocaleController localeController,
      LocalizationValueCache? overlay = null)
  {
    this.localeController = localeController ?? throw new ArgumentNullException(nameof(localeController));
    this.overlay = overlay;
  }

  private readonly LocalizationValueCache? overlay;

  public CultureInfo Culture => localeController.Culture;

  public string Localize(UiMessageKey key) => Resource(key.Value);

  public string Format(UiMessageKey key, params (string Name, object? Value)[] arguments)
  {
    var values = arguments.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
    var resourceKey = key.Value;
    if (UiMessageDescriptors.All.TryGetValue(key, out var descriptor))
    {
      var number = Convert.ToDecimal(
          RequiredArgument(values, descriptor.ValueParameter, key),
          CultureInfo.InvariantCulture);
      var plural = UiCardinalRules.Select(localeController.EffectiveLocale, number);
      if (descriptor.Kind == UiMessageDescriptorKind.Plural)
      {
        resourceKey = $"{resourceKey}.__plural.{plural}";
      }
      else
      {
        var selection = Convert.ToString(
            RequiredArgument(values, descriptor.SelectParameter!, key),
            CultureInfo.InvariantCulture) ?? string.Empty;
        if (!descriptor.Cases.Contains(selection, StringComparer.Ordinal))
        {
          throw new ArgumentOutOfRangeException(
              descriptor.SelectParameter,
              selection,
              $"Message '{key.Value}' does not define that selection.");
        }
        resourceKey = $"{resourceKey}.__select.{selection}.{plural}";
      }
    }

    var result = Resource(resourceKey);
    return PlaceholderPattern().Replace(result, match =>
    {
      var name = match.Groups["name"].Value;
      if (!values.TryGetValue(name, out var value)) return match.Value;
      return value switch
      {
        null => string.Empty,
        UiText text => Resolve(text),
        _ when descriptor?.NumberParameters.Contains(name, StringComparer.Ordinal) is true =>
            FormatNumber(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
        IFormattable formattable => formattable.ToString(null, localeController.Culture),
        _ => value.ToString() ?? string.Empty,
      };
    });
  }

  public string Resolve(UiText text) =>
      text.Key is UiMessageKey key
          ? text.Arguments is { Count: > 0 }
              ? Format(key, [.. text.Arguments])
              : Localize(key)
          : text.NumberValue is decimal number
              ? FormatNumber(number)
              : text.VerbatimValue ?? string.Empty;

  public string FormatDateTime(DateTimeOffset instant, TimeZoneInfo timeZone) =>
      TimeZoneInfo.ConvertTime(
          instant,
          timeZone ?? throw new ArgumentNullException(nameof(timeZone)))
          .ToString("g", localeController.Culture);

  public string FormatNumber(decimal value) =>
      value.ToString("#,0.############################", localeController.Culture);

  public string FormatPercent(decimal value) => value.ToString("0.##%", localeController.Culture);

  public string FormatCurrency(decimal value, string currencyCode)
    => FormatCurrency(value, currencyCode, 2, 2);

  public string FormatCurrency(decimal value, string currencyCode, int maximumFractionDigits)
    => FormatCurrency(value, currencyCode, 0, maximumFractionDigits);

  public string FormatCurrency(BigInteger scaledAmount, string currencyCode, int scale)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(scaledAmount);
    ArgumentOutOfRangeException.ThrowIfNegative(scale);
    ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
    var divisor = BigInteger.Pow(10, scale);
    var whole = BigInteger.DivRem(scaledAmount, divisor, out var remainder);
    var fraction = remainder.ToString(CultureInfo.InvariantCulture).PadLeft(scale, '0').TrimEnd('0');
    var number = whole.ToString("#,0", localeController.Culture);
    if (fraction.Length > 0) number += localeController.Culture.NumberFormat.NumberDecimalSeparator + fraction;
    var code = currencyCode.Trim().ToUpperInvariant();
    return localeController.EffectiveLocale switch
    {
      "es" or "fr" => $"{number}\u00A0{code}",
      _ => $"{code}\u00A0{number}",
    };
  }

  public string FormatCurrency(
      decimal value,
      string currencyCode,
      int minimumFractionDigits,
      int maximumFractionDigits)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
    ArgumentOutOfRangeException.ThrowIfNegative(minimumFractionDigits);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumFractionDigits, 28);
    ArgumentOutOfRangeException.ThrowIfLessThan(maximumFractionDigits, minimumFractionDigits);
    var code = currencyCode.Trim().ToUpperInvariant();
    var fractionFormat = new string('0', minimumFractionDigits)
        + new string('#', maximumFractionDigits - minimumFractionDigits);
    var numberFormat = maximumFractionDigits == 0 ? "#,0" : $"#,0.{fractionFormat}";
    var number = Math.Abs(value).ToString(numberFormat, localeController.Culture);
    var sign = value < 0 ? "-" : string.Empty;
    return localeController.EffectiveLocale switch
    {
      "es" or "fr" => $"{sign}{number}\u00A0{code}",
      _ => $"{sign}{code}\u00A0{number}",
    };
  }

  [GeneratedRegex(@"\{(?<name>[A-Za-z][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant)]
  private static partial Regex PlaceholderPattern();

  private static object? RequiredArgument(
      Dictionary<string, object?> values,
      string name,
      UiMessageKey key) =>
      values.TryGetValue(name, out var value)
          ? value
          : throw new ArgumentException(
              $"Message '{key.Value}' requires an argument named '{name}'.",
              nameof(values));

  private string Resource(string key) =>
      overlay?.Value(key, localeController.EffectiveLocale)
      ?? Resources.GetString(key, localeController.Culture)
      ?? Resources.GetString(key, CultureInfo.GetCultureInfo("en"))
      ?? key;

  private sealed class EnglishLanguageProvider : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["en"];
  }
}
