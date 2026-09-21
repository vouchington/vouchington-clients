using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.FeatureFlags;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.FeatureFlags;

public sealed class FeatureFlagOverridesViewModelTests
{
  [Fact]
  public async Task AuthorizedWorkflowUsesPublicReadAndPublishesImmediateNavigationChanges()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new StubService(new(new Dictionary<string, bool> { ["fediverse"] = false }, new Dictionary<string, bool>()));
    var store = new MemoryStore();
    using var state = new FeatureFlagState(store);
    var session = new SessionStore(UserWithRole("developer"));
    var viewModel = new FeatureFlagOverridesViewModel(service, state, session);
    var provider = new MutableNavigationViewerProvider();
    _ = new FeatureFlagNavigationBinding(state, session, provider);
    var visibleIntentSnapshots = new List<bool>();
    provider.ViewerChanged += (_, args) => visibleIntentSnapshots.Add(
        NavigationCatalog.GetVisibleBottomTabs(args.Viewer).Any(intent => intent.Id == "fediverse"));

    await viewModel.LoadAsync(token);
    await viewModel.SetOverrideAsync("fediverse", true, token);
    await viewModel.SetOverrideAsync("fediverse", false, token);
    await viewModel.RemoveOverrideAsync("fediverse", token);
    await viewModel.SetOverrideAsync("fediverse", true, token);
    await viewModel.ClearOverridesAsync(token);

    Assert.Equal(1, service.FetchCalls);
    Assert.Equal([false, true, false, false, true, false], visibleIntentSnapshots);
    Assert.Equal(5, store.SaveCalls);
    Assert.Empty(state.LocalOverrides);
  }

  [Theory]
  [InlineData("administrator", true)]
  [InlineData("developer", true)]
  [InlineData("moderator", false)]
  [InlineData("investor", false)]
  public async Task AuthorizationMatchesGlobalFeatureFlagOperators(string role, bool authorized)
  {
    var service = new StubService(new(new Dictionary<string, bool> { ["fediverse"] = false }, new Dictionary<string, bool>()));
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new FeatureFlagOverridesViewModel(service, state, new SessionStore(UserWithRole(role)));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(authorized, viewModel.IsAuthorized);
    Assert.Equal(authorized ? 1 : 0, service.FetchCalls);
  }

  [Fact]
  public async Task ReadAndPersistenceErrorsAreVisibleAndDoNotPublishFailedState()
  {
    var token = TestContext.Current.CancellationToken;
    var failingRead = new StubService(new(new Dictionary<string, bool>(), new Dictionary<string, bool>()))
    {
      Failure = new HttpRequestException("offline"),
    };
    using var readState = new FeatureFlagState(new MemoryStore());
    var readViewModel = new FeatureFlagOverridesViewModel(failingRead, readState, new SessionStore(UserWithRole("developer")));
    await readViewModel.LoadAsync(token);
    Assert.Equal(
        UiLocalization.English.Format(
            UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsLoadFlagsFailedReason,
            ("error", UiText.ExternalContent("offline"))),
        readViewModel.ErrorMessage);

    var writeStore = new MemoryStore { Failure = new IOException("disk full") };
    using var writeState = new FeatureFlagState(writeStore);
    var writeViewModel = new FeatureFlagOverridesViewModel(
        new StubService(new(new Dictionary<string, bool> { ["fediverse"] = false }, new Dictionary<string, bool>())),
        writeState,
        new SessionStore(UserWithRole("developer")));
    await writeViewModel.LoadAsync(token);

    Assert.False(await writeViewModel.SetOverrideAsync("fediverse", true, token));
    Assert.Equal(
        UiLocalization.English.Format(
            UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsSaveOverrideFailedReason,
            ("error", UiText.ExternalContent("disk full"))),
        writeViewModel.ErrorMessage);
    Assert.False(writeState.EffectiveFlags["fediverse"]);
  }

  [Fact]
  public async Task DelayedPublicReadCannotOverwriteLaterAuthoritativeWrite()
  {
    var token = TestContext.Current.CancellationToken;
    var service = new DelayedService(new FeatureFlagsResponse(
        new Dictionary<string, bool> { ["fediverse"] = false },
        new Dictionary<string, bool>()));
    using var state = new FeatureFlagState(new MemoryStore());
    var viewModel = new FeatureFlagOverridesViewModel(
        service,
        state,
        new SessionStore(UserWithRole("developer")));

    var load = viewModel.LoadAsync(token);
    await service.FetchStarted.Task.WaitAsync(token);
    await state.ApplyAuthoritativeRemoteAsync(
        new Dictionary<string, bool> { ["fediverse"] = true },
        token);
    service.Release();
    await load;

    Assert.True(state.RemoteFlags["fediverse"]);
    Assert.True(state.EffectiveFlags["fediverse"]);
    Assert.True(Assert.Single(viewModel.Flags).GlobalValue);
  }

  [Fact]
  public async Task ClearAllIncludesStaleOverridesHiddenFromRemoteFlags()
  {
    var token = TestContext.Current.CancellationToken;
    var store = new MemoryStore(new Dictionary<string, bool> { ["removed-flag"] = true });
    using var state = new FeatureFlagState(store);
    var viewModel = new FeatureFlagOverridesViewModel(
        new StubService(new(new Dictionary<string, bool> { ["fediverse"] = false }, new Dictionary<string, bool>())),
        state,
        new SessionStore(UserWithRole("developer")));

    await viewModel.LoadAsync(token);

    Assert.True(viewModel.HasLocalOverrides);
    Assert.DoesNotContain(viewModel.Flags, flag => flag.Key == "removed-flag");
    Assert.True(await viewModel.ClearOverridesAsync(token));
    Assert.False(viewModel.HasLocalOverrides);
    Assert.Empty(state.LocalOverrides);
    Assert.Empty(store.Values);
  }

  private static User UserWithRole(string role) => new("user", "qa", Roles: [role]);

  private sealed class StubService(FeatureFlagsResponse response) : IFeatureFlagService
  {
    public int FetchCalls { get; private set; }
    public Exception? Failure { get; init; }
    public Task<FeatureFlagsResponse> FetchAsync(CancellationToken cancellationToken = default)
    {
      FetchCalls++;
      return Failure is null ? Task.FromResult(response) : Task.FromException<FeatureFlagsResponse>(Failure);
    }
  }

  private sealed class DelayedService(FeatureFlagsResponse response) : IFeatureFlagService
  {
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource FetchStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<FeatureFlagsResponse> FetchAsync(CancellationToken cancellationToken = default)
    {
      FetchStarted.TrySetResult();
      await release.Task.WaitAsync(cancellationToken);
      return response;
    }

    public void Release() => release.TrySetResult();
  }

  private sealed class MemoryStore : IFeatureFlagOverrideStore
  {
    public MemoryStore(IReadOnlyDictionary<string, bool>? values = null) =>
        Values = values ?? new Dictionary<string, bool>();

    public IReadOnlyDictionary<string, bool> Values { get; private set; }
    public int SaveCalls { get; private set; }
    public Exception? Failure { get; init; }
    public Task<IReadOnlyDictionary<string, bool>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Values);
    public Task SaveAsync(IReadOnlyDictionary<string, bool> values, CancellationToken cancellationToken = default)
    {
      if (Failure is not null) return Task.FromException(Failure);
      SaveCalls++;
      Values = new Dictionary<string, bool>(values);
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
}
