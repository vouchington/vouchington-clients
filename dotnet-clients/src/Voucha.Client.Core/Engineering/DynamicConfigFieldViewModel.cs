using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Engineering;

public sealed class DynamicConfigFieldViewModel : ObservableObject
{
  private string draft;
  private UiText? validationText;
  private readonly IUiLocalization localization;
  private readonly CultureInfo? explicitCulture;
  private bool isDraftDirty;

  public DynamicConfigFieldViewModel(DynamicConfigField field, CultureInfo? culture = null)
      : this(field, UiLocalization.English, culture)
  {
  }

  public DynamicConfigFieldViewModel(
      DynamicConfigField field,
      IUiLocalization localization,
      CultureInfo? culture = null)
  {
    Field = field ?? throw new ArgumentNullException(nameof(field));
    this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
    explicitCulture = culture;
    draft = Format(field.Value, Culture);
    validationText = TypeMismatch(field);
  }

  public DynamicConfigField Field { get; private set; }
  public string Name => Field.Name;
  public string ProtocolName => Field.Name;
  public string Description => Field.Description;
  public string ExternalContentDescription => Field.Description;
  public bool IsBoolean => Field.Type == "boolean";
  public bool IsText => Field.Type == "string";
  public bool IsNumber => Field.Type == "number";
  public bool BooleanValue => Field.Value is DynamicConfigBooleanValue value && value.Value;
  public string Draft
  {
    get => draft;
    set
    {
      if (!SetProperty(ref draft, value)) return;
      isDraftDirty = true;
      ValidationText = null;
    }
  }
  public string? ValidationMessage
      => validationText is { } text ? localization.Resolve(text) : null;
  private UiText? ValidationText
  {
    get => validationText;
    set
    {
      if (!SetProperty(ref validationText, value)) return;
      OnPropertyChanged(nameof(ValidationMessage));
      OnPropertyChanged(nameof(HasValidationError));
    }
  }
  public bool HasValidationError => ValidationMessage is not null;

  public bool TryValue(out DynamicConfigValue value)
  {
    if (TypeMismatch(Field) is { } mismatch) return Invalid(mismatch, out value);
    if (IsBoolean)
    {
      value = DynamicConfigValues.From(BooleanValue);
      return true;
    }
    if (IsText)
    {
      value = DynamicConfigValues.From(Draft);
      return true;
    }
    if (!double.TryParse(Draft, NumberStyles.Float, Culture, out var number) || !double.IsFinite(number))
    {
      return Invalid(UiText.Localized(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsFiniteNumber), out value);
    }
    if (Field.IsInteger && number != Math.Truncate(number))
      return Invalid(UiText.Localized(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsWholeNumber), out value);
    if (Field.MinValue is { } minimum && number < minimum)
      return Invalid(UiText.Localized(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsMinimumInput,
          ("value", minimum)), out value);
    if (Field.MaxValue is { } maximum && number > maximum)
      return Invalid(UiText.Localized(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsMaximumInput,
          ("value", maximum)), out value);
    ValidationText = null;
    value = DynamicConfigValues.From(number);
    return true;
  }

  public void SetBoolean(bool value)
  {
    Field = Field with { Value = DynamicConfigValues.From(value) };
    OnPropertyChanged(nameof(BooleanValue));
  }

  public void Apply(DynamicConfigField field)
  {
    ApplyServerField(field);
    draft = Format(field.Value, Culture);
    isDraftDirty = false;
    OnPropertyChanged(nameof(Draft));
  }

  public void ApplyPreservingDraft(DynamicConfigField field)
  {
    ApplyServerField(field);
    _ = TryValue(out _);
  }

  private void ApplyServerField(DynamicConfigField field)
  {
    Field = field ?? throw new ArgumentNullException(nameof(field));
    OnPropertyChanged(nameof(Name));
    OnPropertyChanged(nameof(Description));
    OnPropertyChanged(nameof(IsBoolean));
    OnPropertyChanged(nameof(IsText));
    OnPropertyChanged(nameof(IsNumber));
    ValidationText = TypeMismatch(field);
    OnPropertyChanged(nameof(BooleanValue));
  }

  public void OnUiLocaleChanged()
  {
    if (IsNumber && !isDraftDirty)
    {
      draft = Format(Field.Value, Culture);
      OnPropertyChanged(nameof(Draft));
    }
    OnPropertyChanged(nameof(ValidationMessage));
  }

  private bool Invalid(UiText message, out DynamicConfigValue value)
  {
    ValidationText = message;
    value = DynamicConfigValues.From(0d);
    return false;
  }

  private static string Format(DynamicConfigValue value, CultureInfo culture) => value switch
  {
    DynamicConfigBooleanValue boolean => boolean.Value ? "true" : "false",
    DynamicConfigNumericValue number => number.Value.ToString("R", culture),
    DynamicConfigStringValue text => text.Value,
    _ => string.Empty,
  };

  private CultureInfo Culture => explicitCulture ?? localization.Culture;

  private static UiText? TypeMismatch(DynamicConfigField field) => (field.Type, field.Value) switch
  {
    ("boolean", DynamicConfigBooleanValue) or
    ("number", DynamicConfigNumericValue) or
    ("string", DynamicConfigStringValue) => null,
    _ => UiText.Localized(
        UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsTypeMismatch,
        ("type", UiText.ProtocolValue(field.Type))),
  };
}
