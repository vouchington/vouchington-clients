using System.Globalization;
using Voucha.Client.Core.Auth;

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
}

public sealed class UiLocaleController : IUiLocaleController, IDisposable
{
  private static readonly HashSet<string> SupportedLocales = ["en", "es", "fr", "pt"];
  private readonly IDeviceLanguageProvider deviceLanguageProvider;
  private readonly IUiThreadDispatcher dispatcher;
  private readonly object listenersGate = new();
  private readonly Dictionary<long, WeakReference<IUiLocaleChangeListener>> listeners = [];
  private ISessionStore? sessionStore;
  private long nextListenerId;

  public UiLocaleController(
      IDeviceLanguageProvider deviceLanguageProvider,
      IUiThreadDispatcher? dispatcher = null)
  {
    this.deviceLanguageProvider = deviceLanguageProvider
        ?? throw new ArgumentNullException(nameof(deviceLanguageProvider));
    this.dispatcher = dispatcher ?? new CapturedSynchronizationContextDispatcher();
    EffectiveLocale = ResolveEffectiveLocale(null);
  }

  public event EventHandler? LocaleChanged;

  public string EffectiveLocale { get; private set; }

  public CultureInfo Culture => CultureInfo.GetCultureInfo(EffectiveLocale);

  public void AttachSession(ISessionStore store)
  {
    ArgumentNullException.ThrowIfNull(store);
    if (sessionStore is not null) sessionStore.SessionChanged -= OnSessionChanged;
    sessionStore = store;
    sessionStore.SessionChanged += OnSessionChanged;
    ApplySavedLocale(store.Current.Identity?.UiLocale);
  }

  public void ApplySavedLocale(string? locale)
  {
    if (dispatcher.IsDispatchRequired)
    {
      dispatcher.Dispatch(() => ApplySavedLocaleOnUiThread(locale));
      return;
    }

    ApplySavedLocaleOnUiThread(locale);
  }

  public IDisposable SubscribeLocaleChanges(IUiLocaleChangeListener listener)
  {
    ArgumentNullException.ThrowIfNull(listener);
    var id = Interlocked.Increment(ref nextListenerId);
    lock (listenersGate)
    {
      listeners.Add(id, new WeakReference<IUiLocaleChangeListener>(listener));
    }
    return new LocaleSubscription(this, id);
  }

  private void ApplySavedLocaleOnUiThread(string? locale)
  {
    var next = ResolveEffectiveLocale(locale);
    if (next == EffectiveLocale) return;
    EffectiveLocale = next;
    LocaleChanged?.Invoke(this, EventArgs.Empty);
    NotifyWeakListeners();
  }

  public void Dispose()
  {
    if (sessionStore is not null) sessionStore.SessionChanged -= OnSessionChanged;
    lock (listenersGate)
    {
      listeners.Clear();
    }
  }

  private void OnSessionChanged(object? sender, SessionChangedEventArgs eventArgs) =>
      ApplySavedLocale(eventArgs.Snapshot.Identity?.UiLocale);

  private string ResolveEffectiveLocale(string? locale) =>
      NormalizeSupported(locale)
      ?? deviceLanguageProvider.PreferredLanguages.Select(NormalizeSupported).FirstOrDefault(value => value is not null)
      ?? "en";

  private static string? NormalizeSupported(string? locale)
  {
    if (string.IsNullOrWhiteSpace(locale)) return null;
    var normalized = locale.Trim().Replace('_', '-').Split('-', 2)[0].ToUpperInvariant() switch
    {
      "EN" => "en",
      "ES" => "es",
      "FR" => "fr",
      "PT" => "pt",
      _ => string.Empty,
    };
    return SupportedLocales.Contains(normalized) ? normalized : null;
  }

  private void NotifyWeakListeners()
  {
    List<IUiLocaleChangeListener> liveListeners = [];
    lock (listenersGate)
    {
      foreach (var (id, reference) in listeners.ToArray())
      {
        if (reference.TryGetTarget(out var listener))
        {
          liveListeners.Add(listener);
        }
        else
        {
          listeners.Remove(id);
        }
      }
    }
    foreach (var listener in liveListeners) listener.OnUiLocaleChanged();
  }

  private void RemoveListener(long id)
  {
    lock (listenersGate)
    {
      listeners.Remove(id);
    }
  }

  private sealed class LocaleSubscription(UiLocaleController owner, long id) : IDisposable
  {
    private Action<long>? unsubscribe = owner.RemoveListener;

    public void Dispose() => Interlocked.Exchange(ref unsubscribe, null)?.Invoke(id);
  }

  private sealed class CapturedSynchronizationContextDispatcher : IUiThreadDispatcher
  {
    private readonly SynchronizationContext? context = SynchronizationContext.Current;
    private readonly int capturedThreadId = Environment.CurrentManagedThreadId;

    public bool IsDispatchRequired =>
        context is not null && Environment.CurrentManagedThreadId != capturedThreadId;

    public void Dispatch(Action action)
    {
      ArgumentNullException.ThrowIfNull(action);
      if (context is null || !IsDispatchRequired)
      {
        action();
        return;
      }
      context.Send(_ => action(), null);
    }
  }
}
