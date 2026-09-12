using System.Globalization;

namespace Voucha.Client.Core.Localization;

public interface IDeviceLanguageProvider
{
  IReadOnlyList<string> PreferredLanguages { get; }
}

public interface IUiThreadDispatcher
{
  bool IsDispatchRequired { get; }

  void Dispatch(Action action);
}

public interface IUiLocaleChangeListener
{
  void OnUiLocaleChanged();
}

public interface IUiLocaleController
{
  event EventHandler? LocaleChanged;

  string EffectiveLocale { get; }

  CultureInfo Culture { get; }

  void ApplySavedLocale(string? locale);

  IDisposable SubscribeLocaleChanges(IUiLocaleChangeListener listener);

  void NotifyLocalizedCopyChanged() { }
}
