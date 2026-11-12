using System.Globalization;
using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class EngineeringPostgreSqlPageLifecycleTests
{
  [Fact]
  public async Task DisappearingCancelsPendingConfirmationWithoutDisposingReusableViewModel()
  {
    var (page, model, service, session, locale) = Create();
    using (page)
    {
      var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
      var pending = model.ConfirmRangeAsync(false, false, _ => confirmation.Task, TestContext.Current.CancellationToken);
      Assert.True(model.IsLoading);

      typeof(EngineeringPostgreSqlPage).GetMethod("OnDisappearing", BindingFlags.Instance | BindingFlags.NonPublic)!
          .Invoke(page, null);
      await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

      Assert.False(confirmation.Task.IsCompleted);
      Assert.False(model.IsLoading);
      Assert.False(model.RangeQueued);
      Assert.Equal(0, service.Enqueues);
      Assert.Equal(1, session.Subscriptions);
      Assert.Equal(1, locale.Subscriptions);
      Assert.True(model.IsRangeAuthorized);
      confirmation.SetResult(true);
      await confirmation.Task;
      Assert.Equal(0, service.Enqueues);

      await model.ConfirmRangeAsync(false, false, _ => Task.FromResult(true), TestContext.Current.CancellationToken);
      Assert.Equal(1, service.Enqueues);
      Assert.True(model.RangeQueued);
    }
  }

  [Fact]
  public async Task UnloadedDisposesSubscriptionsAndCancelsPendingConfirmation()
  {
    var (page, model, service, session, locale) = Create();
    var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var pending = model.ConfirmRangeAsync(false, false, _ => confirmation.Task, TestContext.Current.CancellationToken);
    Assert.Equal(1, session.Subscriptions);
    Assert.Equal(1, locale.Subscriptions);

    typeof(EngineeringPostgreSqlPage).GetMethod("OnUnloaded", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(page, [page, EventArgs.Empty]);
    await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    Assert.Equal(0, session.Subscriptions);
    Assert.Equal(0, locale.Subscriptions);
    Assert.False(model.IsRangeAuthorized);
    Assert.False(model.IsLoading);
    confirmation.SetResult(true);
    await confirmation.Task;
    Assert.Equal(0, service.Enqueues);
    page.Dispose();
    Assert.Equal(0, session.Subscriptions);
    Assert.Equal(0, locale.Subscriptions);
  }

  private static (EngineeringPostgreSqlPage Page, EngineeringPostgreSqlViewModel Model,
      ServiceProxy Service, SessionStore Session, LocaleController Locale) Create()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["UiLocaleVersion"] = new UiLocaleVersion(new UiLocaleController(new StubDeviceLanguageProvider("en"))),
        ["UiLocalizedValue"] = new UiLocalizedValueConverter(UiLocalization.English),
        ["Headline"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
      },
    };
    var proxy = DispatchProxy.Create<IEngineeringService, ServiceProxy>();
    var service = (ServiceProxy)(object)proxy;
    var session = new SessionStore();
    var locale = new LocaleController();
    var model = new EngineeringPostgreSqlViewModel(proxy, localeController: locale, sessionStore: session)
    {
      RangeFromDate = "2020-02-01",
      RangeToDate = "2020-02-02",
    };
    return (new EngineeringPostgreSqlPage(model), model, service, session, locale);
  }

  public class ServiceProxy : DispatchProxy
  {
    public int Enqueues { get; private set; }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
      if (targetMethod?.Name != nameof(IEngineeringService.EnqueuePsqlJobAsync))
        throw new InvalidOperationException($"Unexpected call: {targetMethod?.Name}");
      Enqueues++;
      return Task.FromResult(new EngineeringSuccessResponse(true));
    }
  }

  private sealed class SessionStore : ISessionStore
  {
    public int Subscriptions { get; private set; }
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add => Subscriptions++;
      remove => Subscriptions--;
    }
    public SessionSnapshot Current { get; } = new(new User("admin", "admin", Roles: ["administrator"]));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class LocaleController : IUiLocaleController
  {
    public int Subscriptions { get; private set; }
    public event EventHandler? LocaleChanged { add { } remove { } }
    public string EffectiveLocale => "en";
    public CultureInfo Culture => CultureInfo.GetCultureInfo("en");
    public void ApplySavedLocale(string? locale) { }
    public IDisposable SubscribeLocaleChanges(IUiLocaleChangeListener listener)
    {
      Subscriptions++;
      return new Subscription(() => Subscriptions--);
    }
  }

  private sealed class Subscription(Action dispose) : IDisposable
  {
    public void Dispose() => dispose();
  }
}
