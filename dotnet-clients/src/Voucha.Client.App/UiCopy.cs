using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

internal static class UiCopy
{
  public static T Bind<T>(T control, BindableProperty property, UiMessageKey key)
      where T : Element
  {
    control.SetDynamicResource(property, key.Value);
    return control;
  }

  public static string Localize(UiMessageKey key) => Localization.Localize(key);

  public static string Format(
      UiMessageKey key,
      params (string Name, object? Value)[] arguments) => Localization.Format(key, arguments);

  public static string Resolve(UiText text) => Localization.Resolve(text);

  public static string FormatDateTime(DateTimeOffset instant) =>
      Localization.FormatDateTime(instant, TimeZoneInfo.Local);

  public static string FormatNumber(decimal value) => Localization.FormatNumber(value);

  public static string FormatPercent(decimal value) => Localization.FormatPercent(value);

  public static IUiLocalization CurrentLocalization => Localization;

  private static IUiLocalization Localization =>
      (Application.Current as App)?.Localization
      ?? throw new InvalidOperationException("The MAUI application localization service is unavailable.");
}
