using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.PointValuations;

public sealed class PointValuationDraft : ObservableObject
{
  private string valuePerPoint;
  private string note;
  private CultureInfo culture;
  private bool valuePerPointEdited;
  private string currency;
  private readonly IReadOnlyList<string> supportedCurrencies =
      Api.Currency.Supported.Select(value => value.Code).ToArray();

  public PointValuationDraft(CultureInfo culture)
      : this(null, culture) { }

  public PointValuationDraft(PointValuation? original, CultureInfo culture)
  {
    Original = original;
    this.culture = culture ?? throw new ArgumentNullException(nameof(culture));
    valuePerPoint = original?.ValuePerPoint.ToMajorUnits()
        .ToString("0.######", culture) ?? string.Empty;
    note = original?.Note ?? string.Empty;
    currency = original?.ValuePerPoint.Currency ?? "usd";
  }

  public PointValuation? Original { get; }
  public string ValuePerPoint
  {
    get => valuePerPoint;
    set
    {
      valuePerPointEdited = true;
      SetProperty(ref valuePerPoint, value ?? string.Empty);
    }
  }
  public string Note { get => note; set => SetProperty(ref note, value ?? string.Empty); }
  public string Currency { get => currency; set => SetProperty(ref currency, value ?? string.Empty); }
  public IReadOnlyList<string> SupportedCurrencies => supportedCurrencies;

  internal void ApplyCulture(CultureInfo value)
  {
    ArgumentNullException.ThrowIfNull(value);
    if (valuePerPointEdited) return;
    culture = value;
    var formatted = Original?.ValuePerPoint.ToMajorUnits().ToString("0.######", culture) ?? string.Empty;
    SetProperty(ref valuePerPoint, formatted, nameof(ValuePerPoint));
  }

  public bool TryBuildCreate(string rewardsProgramId, out CreatePointValuationBody? body)
  {
    if (!TryParse(out var value)) { body = null; return false; }
    body = new(rewardsProgramId, value, EmptyToNull(Note));
    return true;
  }

  public bool TryBuildUpdate(out UpdatePointValuationBody? body)
  {
    if (Original is null || !TryParse(out var value)) { body = null; return false; }
    var note = EmptyToNull(Note);
    JsonNullableString? noteChange = note == Original.Note
        ? null
        : note is null ? JsonNullableString.Null : JsonNullableString.FromString(note);
    body = new(
        value == Original.ValuePerPoint ? null : value,
        noteChange);
    if (body.ValuePerPoint is null && body.Note is null) body = null;
    return true;
  }

  private bool TryParse(out ScaledMoney value)
  {
    var text = ValuePerPoint.Trim();
    if (HasAtMostScaleFractionDigits(text) &&
        decimal.TryParse(text, NumberStyles.Number, culture, out var majorUnits) &&
        majorUnits >= 0 &&
        majorUnits <= PointValuation.MaximumValuePerPointAmount / 1_000_000m)
    {
      var scaled = majorUnits * 1_000_000m;
      if (scaled == decimal.Truncate(scaled) &&
          scaled <= PointValuation.MaximumValuePerPointAmount)
      {
        if (Api.Currency.MinorUnitExponentFor(Currency) is not null)
        {
          value = new ScaledMoney(decimal.ToInt64(scaled), Currency);
          return true;
        }
      }
    }
    value = null!;
    return false;
  }

  private bool HasAtMostScaleFractionDigits(string value)
  {
    var decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
    var separatorIndex = value.IndexOf(decimalSeparator, StringComparison.Ordinal);
    return separatorIndex < 0 ||
        value.Length - separatorIndex - decimalSeparator.Length <= ScaledMoney.RequiredScale;
  }

  private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;
}
