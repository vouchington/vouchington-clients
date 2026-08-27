namespace Voucha.Client.Core.FeatureFlags;

public sealed class FeatureFlagStateChangedEventArgs(
    IReadOnlyDictionary<string, bool> effectiveFlags) : EventArgs
{
  public IReadOnlyDictionary<string, bool> EffectiveFlags { get; } = effectiveFlags;
}

public readonly record struct FeatureFlagRemoteReadTicket(
    long ReadGeneration,
    long AuthoritativeRevision);

public sealed partial class FeatureFlagState(IFeatureFlagOverrideStore store) : IDisposable
{
  private readonly SemaphoreSlim gate = new(1, 1);
  private readonly Queue<FeatureFlagStateChangedEventArgs> pendingChanges = new();
  private Dictionary<string, bool> remote = new(StringComparer.Ordinal);
  private IReadOnlyDictionary<string, bool> local = new Dictionary<string, bool>(StringComparer.Ordinal);
  private bool changeDrainerActive;
  private long authoritativeRevision;
  private long nextReadGeneration;
  private long latestAppliedReadGeneration;

  public event EventHandler<FeatureFlagStateChangedEventArgs>? Changed;

  public bool IsRemoteHydrated { get; private set; }
  public IReadOnlyDictionary<string, bool> RemoteFlags => remote;
  public IReadOnlyDictionary<string, bool> LocalOverrides => local;
  public IReadOnlyDictionary<string, bool> EffectiveFlags { get; private set; } = new Dictionary<string, bool>(StringComparer.Ordinal);

  public async Task InitializeAsync(CancellationToken cancellationToken = default)
  {
    var drainChanges = false;
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      local = Copy(await store.LoadAsync(cancellationToken).ConfigureAwait(false));
      if (IsRemoteHydrated) drainChanges = Enqueue(Merge());
    }
    finally
    {
      gate.Release();
    }
    if (drainChanges) await DrainChangesAsync().ConfigureAwait(false);
  }

  public async Task<FeatureFlagRemoteReadTicket> BeginRemoteReadAsync(
      CancellationToken cancellationToken = default)
  {
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      return new FeatureFlagRemoteReadTicket(++nextReadGeneration, authoritativeRevision);
    }
    finally
    {
      gate.Release();
    }
  }

  public async Task<bool> ApplyRemoteReadAsync(
      FeatureFlagRemoteReadTicket ticket,
      IReadOnlyDictionary<string, bool> flags,
      CancellationToken cancellationToken = default)
  {
    var drainChanges = false;
    var applied = false;
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (ticket.AuthoritativeRevision != authoritativeRevision ||
          ticket.ReadGeneration < latestAppliedReadGeneration)
      {
        return false;
      }
      latestAppliedReadGeneration = ticket.ReadGeneration;
      drainChanges = Enqueue(ApplyRemote(flags));
      applied = true;
    }
    finally
    {
      gate.Release();
    }
    if (drainChanges) await DrainChangesAsync().ConfigureAwait(false);
    return applied;
  }

  public async Task ApplyAuthoritativeRemoteAsync(
      IReadOnlyDictionary<string, bool> flags,
      CancellationToken cancellationToken = default)
  {
    var drainChanges = false;
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      authoritativeRevision++;
      drainChanges = Enqueue(ApplyRemote(flags));
    }
    finally
    {
      gate.Release();
    }
    if (drainChanges) await DrainChangesAsync().ConfigureAwait(false);
  }

  public Task SetOverrideAsync(string key, bool value, CancellationToken cancellationToken = default) =>
      MutateAsync(next => next[key] = value, cancellationToken, requiredRemoteKey: key);

  public Task RemoveOverrideAsync(string key, CancellationToken cancellationToken = default) =>
      MutateAsync(next => next.Remove(key), cancellationToken);

  public Task ClearOverridesAsync(CancellationToken cancellationToken = default) =>
      MutateAsync(next => next.Clear(), cancellationToken);

  private async Task MutateAsync(
      Action<Dictionary<string, bool>> mutate,
      CancellationToken cancellationToken,
      string? requiredRemoteKey = null)
  {
    var drainChanges = false;
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (requiredRemoteKey is not null && (!IsRemoteHydrated || !remote.ContainsKey(requiredRemoteKey)))
      {
        throw new InvalidOperationException($"Feature flag '{requiredRemoteKey}' is not available.");
      }
      var next = new Dictionary<string, bool>(local, StringComparer.Ordinal);
      mutate(next);
      await store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
      local = next;
      drainChanges = Enqueue(Merge());
    }
    finally
    {
      gate.Release();
    }
    if (drainChanges) await DrainChangesAsync().ConfigureAwait(false);
  }

  private FeatureFlagStateChangedEventArgs Merge()
  {
    var next = new Dictionary<string, bool>(remote, StringComparer.Ordinal);
    if (IsRemoteHydrated)
    {
      foreach (var pair in local)
      {
        if (remote.ContainsKey(pair.Key)) next[pair.Key] = pair.Value;
      }
    }
    EffectiveFlags = next;
    return new FeatureFlagStateChangedEventArgs(next);
  }

  private FeatureFlagStateChangedEventArgs ApplyRemote(IReadOnlyDictionary<string, bool> flags)
  {
    remote = Copy(flags);
    IsRemoteHydrated = true;
    return Merge();
  }

  public void Dispose() => gate.Dispose();

  private static Dictionary<string, bool> Copy(IReadOnlyDictionary<string, bool> value) =>
      new Dictionary<string, bool>(value, StringComparer.Ordinal);
}
