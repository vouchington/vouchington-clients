using Voucha.Client.Core.FeatureFlags;
using Xunit;

namespace Voucha.Client.Core.Tests.FeatureFlags;

public sealed class FeatureFlagStateTests
{
  [Fact]
  public async Task LocalOverridesWinOnlyForRemoteKnownKeys()
  {
    var token = TestContext.Current.CancellationToken;
    var store = new MemoryStore(new Dictionary<string, bool> { ["known"] = false, ["removed"] = true });
    using var state = new FeatureFlagState(store);
    await state.InitializeAsync(token);
    await state.ApplyAuthoritativeRemoteAsync(new Dictionary<string, bool> { ["known"] = true }, token);

    Assert.False(state.EffectiveFlags["known"]);
    Assert.DoesNotContain("removed", state.EffectiveFlags);
  }

  [Fact]
  public async Task ExplicitFalseRemoveAndClearPersistAcrossInstances()
  {
    var token = TestContext.Current.CancellationToken;
    var store = new MemoryStore();
    using var state = new FeatureFlagState(store);
    await state.InitializeAsync(token);
    await state.ApplyAuthoritativeRemoteAsync(new Dictionary<string, bool> { ["fediverse"] = true }, token);
    await state.SetOverrideAsync("fediverse", false, token);
    Assert.False(store.Values["fediverse"]);
    await state.RemoveOverrideAsync("fediverse", token);
    Assert.Empty(store.Values);
    await state.SetOverrideAsync("fediverse", false, token);
    await state.ClearOverridesAsync(token);
    Assert.Empty(store.Values);
  }

  [Fact]
  public async Task FailedPersistenceDoesNotPublishState()
  {
    var token = TestContext.Current.CancellationToken;
    var store = new MemoryStore { Failure = new IOException("disk full") };
    using var state = new FeatureFlagState(store);
    await state.InitializeAsync(token);
    await state.ApplyAuthoritativeRemoteAsync(new Dictionary<string, bool> { ["fediverse"] = false }, token);
    var notifications = 0;
    state.Changed += (_, _) => notifications++;

    await Assert.ThrowsAsync<IOException>(() => state.SetOverrideAsync("fediverse", true, token));
    Assert.False(state.EffectiveFlags["fediverse"]);
    Assert.Equal(0, notifications);
  }

  [Fact]
  public async Task InitializeSerializesWithMutationWithoutOverwritingIt()
  {
    var token = TestContext.Current.CancellationToken;
    var store = new DelayedLoadStore(new Dictionary<string, bool> { ["fediverse"] = false });
    using var state = new FeatureFlagState(store);

    var initialize = state.InitializeAsync(token);
    await store.LoadStarted.Task.WaitAsync(token);
    var mutation = state.RemoveOverrideAsync("fediverse", token);
    store.ReleaseLoad();
    await Task.WhenAll(initialize, mutation);

    Assert.Empty(state.LocalOverrides);
    Assert.Empty(store.Values);
  }

  [Fact]
  public async Task RemoteApplicationSerializesWithOverrideInitialization()
  {
    var token = TestContext.Current.CancellationToken;
    var store = new DelayedLoadStore(new Dictionary<string, bool> { ["fediverse"] = true });
    using var state = new FeatureFlagState(store);

    var initialize = state.InitializeAsync(token);
    await store.LoadStarted.Task.WaitAsync(token);
    var beginRead = state.BeginRemoteReadAsync(token);

    Assert.False(beginRead.IsCompleted);
    store.ReleaseLoad();
    await initialize;
    var ticket = await beginRead;
    Assert.True(await state.ApplyRemoteReadAsync(ticket, new Dictionary<string, bool> { ["fediverse"] = false }, token));

    Assert.False(state.RemoteFlags["fediverse"]);
    Assert.True(state.LocalOverrides["fediverse"]);
    Assert.True(state.EffectiveFlags["fediverse"]);
  }

  [Fact]
  public async Task AuthoritativeWriteRejectsOlderReadButAllowsLaterRefresh()
  {
    var token = TestContext.Current.CancellationToken;
    using var state = new FeatureFlagState(new MemoryStore());
    var staleTicket = await state.BeginRemoteReadAsync(token);

    await state.ApplyAuthoritativeRemoteAsync(new Dictionary<string, bool> { ["fediverse"] = true }, token);
    Assert.False(await state.ApplyRemoteReadAsync(
        staleTicket,
        new Dictionary<string, bool> { ["fediverse"] = false },
        token));
    Assert.True(state.RemoteFlags["fediverse"]);

    var freshTicket = await state.BeginRemoteReadAsync(token);
    Assert.True(await state.ApplyRemoteReadAsync(
        freshTicket,
        new Dictionary<string, bool> { ["fediverse"] = false },
        token));
    Assert.False(state.RemoteFlags["fediverse"]);
  }

  [Fact]
  public async Task NewerPublicReadCompletionRejectsOlderPublicResponse()
  {
    var token = TestContext.Current.CancellationToken;
    using var state = new FeatureFlagState(new MemoryStore());
    var older = await state.BeginRemoteReadAsync(token);
    var newer = await state.BeginRemoteReadAsync(token);

    Assert.True(await state.ApplyRemoteReadAsync(
        newer,
        new Dictionary<string, bool> { ["fediverse"] = true },
        token));
    Assert.False(await state.ApplyRemoteReadAsync(
        older,
        new Dictionary<string, bool> { ["fediverse"] = false },
        token));
    Assert.True(state.RemoteFlags["fediverse"]);
  }

  [Fact]
  public async Task RemoteRemovalWinsBeforeQueuedOverrideValidation()
  {
    var token = TestContext.Current.CancellationToken;
    var store = new DelayedLoadStore();
    using var state = new FeatureFlagState(store);
    await state.ApplyAuthoritativeRemoteAsync(new Dictionary<string, bool> { ["fediverse"] = false }, token);

    var initialize = state.InitializeAsync(token);
    await store.LoadStarted.Task.WaitAsync(token);
    var removeRemote = state.ApplyAuthoritativeRemoteAsync(new Dictionary<string, bool>(), token);
    var setOverride = state.SetOverrideAsync("fediverse", true, token);
    store.ReleaseLoad();

    await Task.WhenAll(initialize, removeRemote);
    await Assert.ThrowsAsync<InvalidOperationException>(() => setOverride);
    Assert.DoesNotContain("fediverse", store.Values);
    Assert.DoesNotContain("fediverse", state.LocalOverrides);
  }

  [Fact]
  public async Task ChangedSnapshotsPublishInOrderWhenSubscriberReentersState()
  {
    var token = TestContext.Current.CancellationToken;
    using var state = new FeatureFlagState(new MemoryStore());
    await state.ApplyAuthoritativeRemoteAsync(new Dictionary<string, bool> { ["fediverse"] = false }, token);
    var notifications = new List<(string Subscriber, bool Value)>();
    state.Changed += (_, changed) =>
    {
      var value = changed.EffectiveFlags["fediverse"];
      notifications.Add(("first", value));
      if (value) state.RemoveOverrideAsync("fediverse", token).GetAwaiter().GetResult();
    };
    state.Changed += (_, changed) =>
        notifications.Add(("second", changed.EffectiveFlags["fediverse"]));

    await state.SetOverrideAsync("fediverse", true, token);

    Assert.Equal([
      ("first", true),
      ("second", true),
      ("first", false),
      ("second", false),
    ], notifications);
    Assert.False(state.EffectiveFlags["fediverse"]);
  }

  [Fact]
  public async Task ThrowingSubscriberDoesNotPoisonDeliveryOrLaterMutations()
  {
    var token = TestContext.Current.CancellationToken;
    using var state = new FeatureFlagState(new MemoryStore());
    await state.ApplyAuthoritativeRemoteAsync(new Dictionary<string, bool> { ["fediverse"] = false }, token);
    var notifications = new List<bool>();
    var throwOnce = true;
    state.Changed += (_, _) =>
    {
      if (!throwOnce) return;
      throwOnce = false;
      throw new InvalidOperationException("subscriber failed");
    };
    state.Changed += (_, changed) => notifications.Add(changed.EffectiveFlags["fediverse"]);

    await Assert.ThrowsAsync<InvalidOperationException>(() => state.SetOverrideAsync("fediverse", true, token));
    await state.RemoveOverrideAsync("fediverse", token);

    Assert.Equal([true, false], notifications);
    Assert.False(state.EffectiveFlags["fediverse"]);
  }

  private sealed class MemoryStore(IReadOnlyDictionary<string, bool>? initial = null) : IFeatureFlagOverrideStore
  {
    public Dictionary<string, bool> Values { get; private set; } = new(initial ?? new Dictionary<string, bool>());
    public Exception? Failure { get; init; }

    public Task<IReadOnlyDictionary<string, bool>> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, bool>>(new Dictionary<string, bool>(Values));

    public Task SaveAsync(IReadOnlyDictionary<string, bool> values, CancellationToken cancellationToken = default)
    {
      if (Failure is not null) return Task.FromException(Failure);
      Values = new Dictionary<string, bool>(values);
      return Task.CompletedTask;
    }
  }

  private sealed class DelayedLoadStore(IReadOnlyDictionary<string, bool>? initial = null) : IFeatureFlagOverrideStore
  {
    private readonly TaskCompletionSource releaseLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource LoadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Dictionary<string, bool> Values { get; private set; } = new(initial ?? new Dictionary<string, bool>());

    public async Task<IReadOnlyDictionary<string, bool>> LoadAsync(CancellationToken cancellationToken = default)
    {
      LoadStarted.TrySetResult();
      await releaseLoad.Task.WaitAsync(cancellationToken);
      return new Dictionary<string, bool>(Values);
    }

    public Task SaveAsync(IReadOnlyDictionary<string, bool> values, CancellationToken cancellationToken = default)
    {
      Values = new Dictionary<string, bool>(values);
      return Task.CompletedTask;
    }

    public void ReleaseLoad() => releaseLoad.TrySetResult();
  }
}
