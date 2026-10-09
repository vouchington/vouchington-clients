using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

internal static class UiCopy
{
  private static IUiLocalization localization = UiLocalization.English;

  public static IUiLocalization CurrentLocalization => localization;

  public static void UseLocalization(IUiLocalization value) =>
      localization = value ?? throw new ArgumentNullException(nameof(value));

  public static IDisposable PushLocalization(IUiLocalization value)
  {
    ArgumentNullException.ThrowIfNull(value);
    var previous = localization;
    localization = value;
    return new LocalizationScope(previous);
  }

  public static T Bind<T>(T control, BindableProperty property, UiMessageKey key)
      where T : Element
  {
    control.SetDynamicResource(property, key.Value);
    return control;
  }

  public static string Localize(UiMessageKey key) =>
      localization.Localize(key);

  public static string Format(
      UiMessageKey key,
      params (string Name, object? Value)[] arguments) =>
      localization.Format(key, arguments);

  public static string FormatDateTime(DateTimeOffset instant) =>
      localization.FormatDateTime(instant, TimeZoneInfo.Local);

  public static string Resolve(UiText text) => localization.Resolve(text);

  public static string FormatNumber(decimal value) =>
      localization.FormatNumber(value);

  public static string FormatPercent(decimal value) =>
      localization.FormatPercent(value);

  private sealed class LocalizationScope(IUiLocalization previous) : IDisposable
  {
    private IUiLocalization? restore = previous;

    public void Dispose()
    {
      if (restore is not { } value) return;
      localization = value;
      restore = null;
    }
  }
}
