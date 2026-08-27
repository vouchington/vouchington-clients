using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.PaymentCards;

public sealed class PaymentCardDraft : INotifyPropertyChanged
{
  private DateOnly? openedOn;
  private DateOnly? closedOn;
  private DateOnly? receivedSignUpBonusOn;
  private DateOnly? rememberedOpenedOn;
  private DateOnly? rememberedClosedOn;
  private DateOnly? rememberedSignUpBonusOn;
  private string creditLimit;
  private bool isAuthorizedUser;
  private string? authorizedUserOfId;
  private string note;
  private readonly Money? originalCreditLimit;
  private CultureInfo creditLimitCulture;
  private bool creditLimitEdited;
  private string creditLimitCurrency;
  private readonly IReadOnlyList<string> supportedCurrencies =
      Currency.Supported.Select(value => value.Code).ToArray();

  internal PaymentCardDraft(PaymentCard card, CultureInfo culture)
  {
    Original = card;
    openedOn = card.OpenedOn;
    closedOn = card.ClosedOn;
    receivedSignUpBonusOn = card.ReceivedSignUpBonusOn;
    rememberedOpenedOn = card.OpenedOn;
    rememberedClosedOn = card.ClosedOn;
    rememberedSignUpBonusOn = card.ReceivedSignUpBonusOn;
    originalCreditLimit = card.CreditLimit;
    creditLimitCulture = culture;
    creditLimit = FormatCreditLimit(culture);
    creditLimitCurrency = card.CreditLimit?.Currency ?? "usd";
    isAuthorizedUser = card.IsAuthorizedUser;
    authorizedUserOfId = card.AuthorizedUserOfId;
    note = card.Note ?? string.Empty;
  }

  public event PropertyChangedEventHandler? PropertyChanged;
  public PaymentCard Original { get; }
  public DateOnly? OpenedOn { get => openedOn; set => SetDate(ref openedOn, ref rememberedOpenedOn, value, nameof(HasOpenedOn), nameof(OpenedOnDate)); }
  public DateOnly? ClosedOn { get => closedOn; set => SetDate(ref closedOn, ref rememberedClosedOn, value, nameof(HasClosedOn), nameof(ClosedOnDate)); }
  public DateOnly? ReceivedSignUpBonusOn { get => receivedSignUpBonusOn; set => SetDate(ref receivedSignUpBonusOn, ref rememberedSignUpBonusOn, value, nameof(HasBonusOn), nameof(BonusOnDate)); }
  public bool HasOpenedOn { get => OpenedOn is not null; set => OpenedOn = value ? rememberedOpenedOn ?? Today() : null; }
  public bool HasClosedOn { get => ClosedOn is not null; set => ClosedOn = value ? rememberedClosedOn ?? Today() : null; }
  public bool HasBonusOn { get => ReceivedSignUpBonusOn is not null; set => ReceivedSignUpBonusOn = value ? rememberedSignUpBonusOn ?? Today() : null; }
  public DateTime OpenedOnDate { get => ToDateTime(OpenedOn); set => OpenedOn = DateOnly.FromDateTime(value); }
  public DateTime ClosedOnDate { get => ToDateTime(ClosedOn); set => ClosedOn = DateOnly.FromDateTime(value); }
  public DateTime BonusOnDate { get => ToDateTime(ReceivedSignUpBonusOn); set => ReceivedSignUpBonusOn = DateOnly.FromDateTime(value); }
  public string CreditLimit
  {
    get => creditLimit;
    set
    {
      creditLimitEdited = true;
      Set(ref creditLimit, value ?? string.Empty);
    }
  }
  public bool IsAuthorizedUser { get => isAuthorizedUser; set { if (Set(ref isAuthorizedUser, value) && !value) AuthorizedUserOfId = null; } }
  public string? AuthorizedUserOfId { get => authorizedUserOfId; set => Set(ref authorizedUserOfId, value); }
  public string Note { get => note; set => Set(ref note, value ?? string.Empty); }
  public string CreditLimitCurrency { get => creditLimitCurrency; set => Set(ref creditLimitCurrency, value ?? string.Empty); }
  public IReadOnlyList<string> SupportedCurrencies => supportedCurrencies;

  internal void ApplyCulture(CultureInfo culture)
  {
    if (creditLimitEdited) return;
    creditLimitCulture = culture;
    var formatted = FormatCreditLimit(culture);
    if (creditLimit == formatted) return;
    creditLimit = formatted;
    OnPropertyChanged(nameof(CreditLimit));
  }

  internal bool TryParseCreditLimit(out Money? value)
  {
    if (string.IsNullOrWhiteSpace(creditLimit)) { value = null; return true; }
    var currency = CreditLimitCurrency;
    if (Currency.MinorUnitExponentFor(currency) is { } exponent &&
        decimal.TryParse(creditLimit, NumberStyles.Number, creditLimitCulture, out var parsed) &&
        parsed >= 0)
    {
      var minorUnits = parsed * Money.Pow10(exponent);
      if (minorUnits == decimal.Truncate(minorUnits) &&
          minorUnits <= MoneyContract.MaximumAmount)
      {
        value = new Money(decimal.ToInt64(minorUnits), currency);
        return true;
      }
    }
    value = null;
    return false;
  }

  private string FormatCreditLimit(CultureInfo culture) =>
      originalCreditLimit?.ToKnownCurrencyMajorUnits()?.ToString(culture) ?? string.Empty;

  private static DateTime ToDateTime(DateOnly? value) => (value ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
  private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);

  private void SetDate(ref DateOnly? field, ref DateOnly? remembered, DateOnly? value, string hasName, string dateName)
  {
    if (value is not null) remembered = value;
    if (field == value) return;
    field = value;
    OnPropertyChanged();
    OnPropertyChanged(hasName);
    OnPropertyChanged(dateName);
  }

  private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value;
    OnPropertyChanged(name);
    return true;
  }

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
