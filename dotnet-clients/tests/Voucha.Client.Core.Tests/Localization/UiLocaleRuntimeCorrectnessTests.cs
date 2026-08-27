using System.Collections.Concurrent;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Localization;

public sealed class UiLocaleRuntimeCorrectnessTests
{
  [Fact]
  public async Task WorkerThreadSessionUpdatesNotifyOnTheCapturedUiDispatcher()
  {
    using var dispatcher = new DedicatedThreadDispatcher();
    using var controller = new UiLocaleController(
        new StubDeviceLanguageProvider("en-US"),
        dispatcher);
    var sessionStore = new StubSessionStore();
    var notification = new TaskCompletionSource<int>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    controller.LocaleChanged += (_, _) =>
        notification.TrySetResult(Environment.CurrentManagedThreadId);
    controller.AttachSession(sessionStore);

    await Task.Run(() =>
        sessionStore.SetIdentity(new User("user-1", "alice", UiLocale: "fr")),
        TestContext.Current.CancellationToken);

    Assert.Equal(dispatcher.ThreadId, await notification.Task.WaitAsync(
        TestContext.Current.CancellationToken));
    Assert.Equal("fr", controller.EffectiveLocale);
  }

  [Fact]
  public void WeakLocaleSubscriptionsDoNotKeepTransientConsumersAlive()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en-US"));
    var consumer = CreateTransientConsumer(controller);

    ForceCollection();

    Assert.False(consumer.IsAlive);
    controller.ApplySavedLocale("es");
  }

  [Theory]
  [InlineData("en", 0, "other")]
  [InlineData("en", 0.5, "other")]
  [InlineData("en", 1, "one")]
  [InlineData("en", 1.2, "other")]
  [InlineData("en", 2, "other")]
  [InlineData("es", 0, "other")]
  [InlineData("es", 0.5, "other")]
  [InlineData("es", 1, "one")]
  [InlineData("es", 1.2, "other")]
  [InlineData("es", 2, "other")]
  [InlineData("fr", 0, "one")]
  [InlineData("fr", 0.5, "one")]
  [InlineData("fr", 1, "one")]
  [InlineData("fr", 1.2, "one")]
  [InlineData("fr", 2, "other")]
  [InlineData("pt", 0, "one")]
  [InlineData("pt", 0.5, "one")]
  [InlineData("pt", 1, "one")]
  [InlineData("pt", 1.2, "one")]
  [InlineData("pt", 2, "other")]
  [InlineData("en", -1, "one")]
  [InlineData("fr", -0.5, "one")]
  [InlineData("pt", -2, "other")]
  public void CardinalPluralSelectionMatchesIntl(
      string locale,
      decimal value,
      string expectedCategory)
  {
    Assert.Equal(expectedCategory, UiCardinalRules.Select(locale, value));
  }

  [Fact]
  public void PlaceholderInterpolationIsSinglePass()
  {
    var localization = new UiLocalization(
        new UiLocaleController(new StubDeviceLanguageProvider("en")));

    var result = localization.Format(
        UiMessageKey.NativeDotnetResidualAuthorOnSource,
        ("author", "{source}"),
        ("source", "Voucha"));

    Assert.Equal("{source} on Voucha", result);
  }

  [Theory]
  [InlineData("en", "USD\u00A012.50")]
  [InlineData("es", "12,50\u00A0USD")]
  [InlineData("fr", "12,50\u00A0USD")]
  [InlineData("pt", "USD\u00A012,50")]
  public void CurrencyFormattingMatchesIntlCodePlacement(
      string locale,
      string expected)
  {
    var localization = new UiLocalization(
        new UiLocaleController(new StubDeviceLanguageProvider(locale)));

    Assert.Equal(expected, localization.FormatCurrency(12.5m, "USD"));
  }

  [Theory]
  [InlineData("en", "USD\u00A00.035")]
  [InlineData("fr", "0,035\u00A0USD")]
  public void CurrencyFormattingCanPreserveScaleSixPrecisionWithoutTrailingZeroes(
      string locale,
      string expected)
  {
    var localization = new UiLocalization(
        new UiLocaleController(new StubDeviceLanguageProvider(locale)));

    Assert.Equal(expected, localization.FormatCurrency(0.035m, "USD", 6));
    Assert.DoesNotContain("1.500000", localization.FormatCurrency(1.5m, "USD", 6));
  }

  [Fact]
  public void PreferredLanguageOrderingAllowsASecondarySupportedLocale()
  {
    var languages = DeviceLanguagePreferences.Ordered(
        ["de-DE", "fr-CA", "en-US"],
        "pt-BR");
    var controller = new UiLocaleController(
        new ListDeviceLanguageProvider(languages));

    Assert.Equal(["de-DE", "fr-CA", "en-US", "pt-BR"], languages);
    Assert.Equal("fr", controller.EffectiveLocale);
  }

  [Fact]
  public void DisposedLocaleSubscriptionStopsNotifications()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en-US"));
    var consumer = new CountingLocaleConsumer();
    var subscription = controller.SubscribeLocaleChanges(consumer);

    controller.ApplySavedLocale("es");
    subscription.Dispose();
    controller.ApplySavedLocale("fr");

    Assert.Equal(1, consumer.ChangeCount);
  }

  private static WeakReference CreateTransientConsumer(IUiLocaleController controller)
  {
    var consumer = new TransientLocaleConsumer(controller);
    return new WeakReference(consumer);
  }

  private static void ForceCollection()
  {
    for (var attempt = 0; attempt < 3; attempt++)
    {
      GC.Collect();
      GC.WaitForPendingFinalizers();
      GC.Collect();
    }
  }

  private sealed class TransientLocaleConsumer : IUiLocaleChangeListener
  {
    private readonly IDisposable subscription;

    public TransientLocaleConsumer(IUiLocaleController controller) =>
        subscription = controller.SubscribeLocaleChanges(this);

    public void OnUiLocaleChanged()
    {
    }
  }

  private sealed class CountingLocaleConsumer : IUiLocaleChangeListener
  {
    public int ChangeCount { get; private set; }

    public void OnUiLocaleChanged() => ChangeCount++;
  }

  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }

  private sealed class ListDeviceLanguageProvider(IReadOnlyList<string> languages)
      : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = languages;
  }

  private sealed class StubSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionSnapshot Current { get; private set; } = SessionSnapshot.Anonymous;

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
      SetIdentity(null);
      return Task.CompletedTask;
    }

    public void SetIdentity(User? identity)
    {
      Current = new SessionSnapshot(identity);
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(Current));
    }
  }

  private sealed class DedicatedThreadDispatcher : IUiThreadDispatcher, IDisposable
  {
    private readonly BlockingCollection<Action> actions = [];
    private readonly Thread thread;

    public DedicatedThreadDispatcher()
    {
      thread = new Thread(() =>
      {
        ThreadId = Environment.CurrentManagedThreadId;
        foreach (var action in actions.GetConsumingEnumerable()) action();
      });
      thread.Start();
      while (ThreadId == 0) Thread.Yield();
    }

    public int ThreadId { get; private set; }

    public bool IsDispatchRequired =>
        Environment.CurrentManagedThreadId != ThreadId;

    public void Dispatch(Action action)
    {
      using var completed = new ManualResetEventSlim();
      actions.Add(() =>
      {
        try
        {
          action();
        }
        finally
        {
          completed.Set();
        }
      });
      completed.Wait();
    }

    public void Dispose()
    {
      actions.CompleteAdding();
      thread.Join();
      actions.Dispose();
    }
  }
}
