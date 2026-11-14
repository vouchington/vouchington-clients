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

  [Fact]
  public void AdministratorSeesBothRangeFamiliesBoundToTheirOwnDrafts()
  {
    var (page, model, _, _, _) = Create();
    using (page)
    {
      var entries = RangeEntries(page);
      Assert.Equal(6, entries.Count);
      Assert.All(entries, entry => Assert.True(IsInVisibleBorder(entry)));
      Assert.Equal(["2020-02-01", "2020-02-02"], entries.Take(2).Select(entry => entry.Text));
      model.RequestRangeFromDate = "2021-03-04";
      model.RangeMaxDefaultRows = "9";
      Assert.Equal("2021-03-04", entries[3].Text);
      Assert.Equal("9", entries[2].Text);
      Assert.Equal(new[] { 2, 5 }, entries.Select((entry, index) => (entry, index))
          .Where(pair => pair.entry.Keyboard == Keyboard.Numeric).Select(pair => pair.index));
    }
  }

  [Fact]
  public void NonAdministratorDoesNotSeeRangeControls()
  {
    var (page, _, _, _, _) = Create("moderator");
    using (page)
    {
      var entries = RangeEntries(page);
      Assert.Equal(6, entries.Count);
      Assert.All(entries, entry => Assert.False(IsInVisibleBorder(entry)));
    }
  }

  [Fact]
  public void RangeActionsAreWiredToDistinctHandlers()
  {
    var xaml = AppSource.Read("Pages", "EngineeringPostgreSqlPage.xaml");
    var code = AppSource.Read("Pages", "EngineeringPostgreSqlPage.xaml.cs");
    foreach (var handler in new[]
    {
      "OnClassifierRangeCreateClicked", "OnClassifierRangeRetireClicked",
      "OnRequestRangeCreateClicked", "OnRequestRangeRetireClicked",
    })
    {
      Assert.Contains($"Clicked=\"{handler}\"", xaml, StringComparison.Ordinal);
      Assert.Contains($"void {handler}(", code, StringComparison.Ordinal);
    }
    Assert.Contains("RangeConfirmationMessage(body)", code, StringComparison.Ordinal);
  }

  private static List<Entry> RangeEntries(EngineeringPostgreSqlPage page) =>
      VisualTreeElements.Descendants<Entry>(page).Where(entry => entry.AutomationId is null && IsInRangeBorder(entry)).ToList();

  private static bool IsInRangeBorder(Entry entry) =>
      entry.Parent?.Parent is Border border && border.Content is VerticalStackLayout stack && stack.Children.Count == 8;

  private static bool IsInVisibleBorder(Entry entry) => ((Border)entry.Parent!.Parent!).IsVisible;

  private static (EngineeringPostgreSqlPage Page, EngineeringPostgreSqlViewModel Model,
      ServiceProxy Service, SessionStore Session, LocaleController Locale) Create(string role = "administrator")
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
    var session = new SessionStore(role);
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

  private sealed class SessionStore(string role) : ISessionStore
  {
    public int Subscriptions { get; private set; }
    public event EventHandler<SessionChangedEventArgs>? SessionChanged
    {
      add => Subscriptions++;
      remove => Subscriptions--;
    }
    public SessionSnapshot Current { get; } = new(new User("admin", "admin", Roles: [role]));
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
