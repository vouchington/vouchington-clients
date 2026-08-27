using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.SpendingCategories;

public sealed class SpendingCategoryDraft : INotifyPropertyChanged
{
  private string amount;
  private string frequency;
  private string note;
  private CultureInfo culture;
  private bool amountEdited;
  private string currency;
  private readonly IReadOnlyList<string> supportedCurrencies =
      Api.Currency.Supported.Select(value => value.Code).ToArray();

  public SpendingCategoryDraft(CultureInfo culture) : this(null, culture) { }

  public SpendingCategoryDraft(SpendingCategory? original, CultureInfo culture)
  {
    Original = original;
    this.culture = culture ?? throw new ArgumentNullException(nameof(culture));
    amount = original?.Amount.ToKnownCurrencyMajorUnits()
        ?.ToString("0.############################", culture) ?? string.Empty;
    currency = original?.Amount.Currency ?? "usd";
    frequency = original?.SpendingFrequency ?? "monthly";
    note = original?.Note ?? string.Empty;
  }

  public event PropertyChangedEventHandler? PropertyChanged;
  public SpendingCategory? Original { get; }
  public string Amount { get => amount; set { amountEdited = true; Set(ref amount, value ?? string.Empty); } }
  public string Currency { get => currency; set => Set(ref currency, value ?? string.Empty); }
  public string Frequency { get => frequency; set => Set(ref frequency, value ?? string.Empty); }
  public string Note { get => note; set => Set(ref note, value ?? string.Empty); }
  public IReadOnlyList<string> SupportedCurrencies => supportedCurrencies;

  internal void ApplyCulture(CultureInfo value)
  {
    ArgumentNullException.ThrowIfNull(value);
    if (amountEdited) return;
    culture = value;
    var formatted = Original?.Amount.ToKnownCurrencyMajorUnits()
        ?.ToString("0.############################", culture) ?? string.Empty;
    Set(ref amount, formatted, nameof(Amount));
  }

  public bool TryBuildCreate(string topicId, out CreateSpendingCategoryBody? body)
  {
    if (!TryParse(out var value) || !ValidFrequency()) { body = null; return false; }
    body = new(topicId, value, Frequency, EmptyToNull(Note));
    return true;
  }

  public bool TryBuildUpdate(out UpdateSpendingCategoryBody? body)
  {
    if (Original is null || !TryParse(out var value) || !ValidFrequency()) { body = null; return false; }
    var noteValue = EmptyToNull(Note);
    JsonNullableString? noteChange = noteValue == Original.Note ? null : noteValue is null ? JsonNullableString.Null : JsonNullableString.FromString(noteValue);
    body = new(value == Original.Amount ? null : value,
        Frequency == Original.SpendingFrequency ? null : Frequency, noteChange);
    if (body.Amount is null && body.SpendingFrequency is null && body.Note is null) body = null;
    return true;
  }

  internal bool IsValid => TryParse(out _) && ValidFrequency();
  private bool TryParse(out Money value)
  {
    if (decimal.TryParse(Amount.Trim(), NumberStyles.Number, culture, out var majorUnits) &&
        majorUnits >= 0 &&
        Api.Currency.MinorUnitExponentFor(Currency) is { } exponent)
    {
      var scaleFactor = Money.Pow10(exponent);
      if (majorUnits <= MoneyContract.MaximumAmount / scaleFactor)
      {
        var scaled = majorUnits * scaleFactor;
        if (scaled == decimal.Truncate(scaled))
        {
          value = new Money(decimal.ToInt64(scaled), Currency);
          return true;
        }
      }
    }
    value = null!;
    return false;
  }
  private bool ValidFrequency() => Frequency is "monthly" or "annually";
  private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;
  private void Set(ref string field, string value, [CallerMemberName] string? name = null)
  { if (field == value) return; field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
}
