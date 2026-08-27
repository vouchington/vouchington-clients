using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.FeatureFlags;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class FeatureFlagOverridesPageTests
{
  [Fact]
  public async Task StandalonePageRendersOverrideStatesWithoutDynamicConfigOrWebDependencies()
  {
    var token = TestContext.Current.CancellationToken;
    var featureService = new FeatureFlagService();
    var dynamicService = new ThrowingDynamicConfigService();
    var store = new MemoryStore();
    using var state = new FeatureFlagState(store);
    var viewModel = ViewModel(featureService, state);
    await viewModel.LoadAsync(token);
    var page = CreatePage(viewModel);

    var picker = Find<Picker>(page, "feature-flag-fediverse-override");
    Assert.Equal(
        new[]
        {
          UiLocalization.English.Localize(UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsInherited),
          UiLocalization.English.Localize(UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsEnabled),
          UiLocalization.English.Localize(UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsDisabled),
        },
        Assert.IsAssignableFrom<IEnumerable>(picker.ItemsSource).Cast<string>());
    Assert.Equal(0, picker.SelectedIndex);
    picker.SelectedIndex = 1;
    await WaitUntilAsync(() => state.LocalOverrides.TryGetValue("fediverse", out var value) && value);
    Find<Picker>(page, "feature-flag-fediverse-override").SelectedIndex = 2;
    await WaitUntilAsync(() => state.LocalOverrides.TryGetValue("fediverse", out var value) && !value);
    Find<Picker>(page, "feature-flag-fediverse-override").SelectedIndex = 0;
    await WaitUntilAsync(() => !state.LocalOverrides.ContainsKey("fediverse"));
    Find<Picker>(page, "feature-flag-fediverse-override").SelectedIndex = 1;
    await WaitUntilAsync(() => state.LocalOverrides.ContainsKey("fediverse"));
    Find<Button>(page, "feature-flag-clear-overrides").SendClicked();
    await WaitUntilAsync(() => state.LocalOverrides.Count == 0);

    Assert.Equal(1, featureService.FetchCalls);
    Assert.Equal(0, dynamicService.Calls);
    Assert.Empty(Descendants<WebView>(page));
  }

  [Theory]
  [InlineData("administrator", true)]
  [InlineData("developer", true)]
  [InlineData("moderator", false)]
  [InlineData("customer_support", false)]
  [InlineData("investor", false)]
  public void EngineeringEntryIsRoleGatedAndResolvesWithoutDynamicConfig(string role, bool expected)
  {
    using var state = new FeatureFlagState(new MemoryStore());
    var page = CreatePage(ViewModel(new FeatureFlagService(), state, role));
    var dynamicService = new ThrowingDynamicConfigService();
    var services = new ServiceCollection()
        .AddSingleton(page)
        .AddSingleton<IDynamicConfigService>(dynamicService)
        .BuildServiceProvider();
    var session = new SessionStore(new User("user", "qa", Roles: [role]));

    var resolved = FeatureFlagOverridesPageFactory.TryCreate(services, session, out var result);

    Assert.Equal(expected, resolved);
    Assert.Equal(expected ? page : null, result);
    Assert.Equal(0, dynamicService.Calls);
  }

  [Fact]
  public async Task StandalonePageRendersReadAndPersistenceErrors()
  {
    var token = TestContext.Current.CancellationToken;
    using var unauthorizedState = new FeatureFlagState(new MemoryStore());
    var unauthorizedService = new FeatureFlagService();
    var unauthorizedViewModel = ViewModel(unauthorizedService, unauthorizedState, "moderator");
    await unauthorizedViewModel.LoadAsync(token);
    var unauthorizedPage = CreatePage(unauthorizedViewModel);
    Assert.True(Find<Label>(unauthorizedPage, "feature-flag-access-required").IsVisible);
    Assert.Empty(Descendants<Picker>(unauthorizedPage));
    Assert.Equal(0, unauthorizedService.FetchCalls);

    using var readState = new FeatureFlagState(new MemoryStore());
    var readViewModel = ViewModel(new FeatureFlagService { Failure = new HttpRequestException("offline") }, readState);
    await readViewModel.LoadAsync(token);
    var readPage = CreatePage(readViewModel);
    Assert.True(Find<Border>(readPage, "feature-flag-error").IsVisible);
    var readError = UiLocalization.English.Format(
        UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsLoadFlagsFailedReason,
        ("error", UiText.ExternalContent("offline")));
    Assert.Contains(Descendants<Label>(readPage), label => label.Text == readError);

    using var writeState = new FeatureFlagState(new MemoryStore { Failure = new IOException("disk full") });
    var writeViewModel = ViewModel(new FeatureFlagService(), writeState);
    await writeViewModel.LoadAsync(token);
    var writePage = CreatePage(writeViewModel);
    Find<Picker>(writePage, "feature-flag-fediverse-override").SelectedIndex = 1;
    await WaitUntilAsync(() => writeViewModel.HasError);
    var writeError = UiLocalization.English.Format(
        UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsSaveOverrideFailedReason,
        ("error", UiText.ExternalContent("disk full")));
    Assert.Contains(Descendants<Label>(writePage), label => label.Text == writeError);
    Assert.Equal(0, Find<Picker>(writePage, "feature-flag-fediverse-override").SelectedIndex);
  }

  [Fact]
  public async Task ClearAllIncludesPersistedOverridesMissingFromRemoteFlags()
  {
    var store = new MemoryStore(new Dictionary<string, bool> { ["removed-flag"] = true });
    using var state = new FeatureFlagState(store);
    var viewModel = ViewModel(new FeatureFlagService(), state);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(viewModel);

    var clear = Find<Button>(page, "feature-flag-clear-overrides");
    Assert.True(clear.IsEnabled);
    Assert.DoesNotContain(viewModel.Flags, flag => flag.Key == "removed-flag");

    clear.SendClicked();
    await WaitUntilAsync(() => !viewModel.HasLocalOverrides);

    Assert.Empty(state.LocalOverrides);
    Assert.False(Find<Button>(page, "feature-flag-clear-overrides").IsEnabled);
  }

  private static FeatureFlagOverridesViewModel ViewModel(
      IFeatureFlagService service,
      FeatureFlagState state,
      string role = "developer") =>
      new(service, state, new SessionStore(new User("user", "qa", Roles: [role])));

  private static FeatureFlagOverridesPage CreatePage(FeatureFlagOverridesViewModel viewModel)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var application = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
      },
    };
    foreach (var key in UiMessageKey.All)
      application.Resources[key.Value] = UiLocalization.English.Localize(key);
    return new FeatureFlagOverridesPage(viewModel);
  }

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == automationId);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(10, TestContext.Current.CancellationToken);
    Assert.True(condition());
  }

  private sealed class FeatureFlagService : IFeatureFlagService
  {
    public int FetchCalls { get; private set; }
    public Exception? Failure { get; init; }
    public Task<FeatureFlagsResponse> FetchAsync(CancellationToken cancellationToken = default)
    {
      FetchCalls++;
      return Failure is null
          ? Task.FromResult(new FeatureFlagsResponse(new Dictionary<string, bool> { ["fediverse"] = false }, new Dictionary<string, bool>()))
          : Task.FromException<FeatureFlagsResponse>(Failure);
    }
  }

  private sealed class MemoryStore : IFeatureFlagOverrideStore
  {
    private IReadOnlyDictionary<string, bool> values;
    public MemoryStore(IReadOnlyDictionary<string, bool>? values = null) =>
        this.values = values ?? new Dictionary<string, bool>();
    public Exception? Failure { get; init; }
    public Task<IReadOnlyDictionary<string, bool>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(values);
    public Task SaveAsync(IReadOnlyDictionary<string, bool> newValues, CancellationToken cancellationToken = default)
    {
      if (Failure is not null) return Task.FromException(Failure);
      values = new Dictionary<string, bool>(newValues);
      return Task.CompletedTask;
    }
  }

  private sealed class SessionStore(User identity) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(identity);
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class ThrowingDynamicConfigService : IDynamicConfigService
  {
    public int Calls { get; private set; }
    private Task<T> Fail<T>()
    {
      Calls++;
      return Task.FromException<T>(new InvalidOperationException("Dynamic Config unavailable"));
    }
    public Task<DynamicConfigNamespacesResponse> FetchNamespacesAsync(CancellationToken cancellationToken = default) => Fail<DynamicConfigNamespacesResponse>();
    public Task<DynamicConfigNamespaceResponse> FetchNamespaceAsync(string namespaceName, CancellationToken cancellationToken = default) => Fail<DynamicConfigNamespaceResponse>();
    public Task<DynamicConfigUpdateResponse> UpdateFieldAsync(string namespaceName, string field, DynamicConfigValue value, CancellationToken cancellationToken = default) => Fail<DynamicConfigUpdateResponse>();
    public Task<DynamicConfigHistoryResponse> FetchHistoryAsync(string namespaceName, CancellationToken cancellationToken = default) => Fail<DynamicConfigHistoryResponse>();
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => new ImmediateDispatcherTimer();
  }

  private sealed class ImmediateDispatcherTimer : IDispatcherTimer
  {
    public TimeSpan Interval { get; set; }
    public bool IsRepeating { get; set; }
    public bool IsRunning { get; private set; }
    public event EventHandler? Tick;
    public void Start() { IsRunning = true; Tick?.Invoke(this, EventArgs.Empty); if (!IsRepeating) IsRunning = false; }
    public void Stop() => IsRunning = false;
  }
}
