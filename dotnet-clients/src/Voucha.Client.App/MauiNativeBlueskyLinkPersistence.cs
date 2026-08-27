using System.Text.Json;
using Voucha.Client.Core.Fediverse;

namespace Voucha.Client.App;

public sealed class MauiNativeBlueskyLinkPersistence : INativeBlueskyLinkPersistence, IDisposable
{
  private const string StorageKey = "native-bluesky-link-v1";
  private readonly SemaphoreSlim gate = new(1, 1);

  public async Task<PendingNativeBlueskyLink?> ReadAsync(CancellationToken cancellationToken = default)
  {
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try { return await ReadUnlockedAsync().ConfigureAwait(false); }
    finally { gate.Release(); }
  }

  public async Task WriteAsync(PendingNativeBlueskyLink pending, CancellationToken cancellationToken = default)
  {
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try { await SecureStorage.Default.SetAsync(StorageKey, JsonSerializer.Serialize(pending)).ConfigureAwait(false); }
    finally { gate.Release(); }
  }

  public async Task<bool> ClaimCallbackAsync(
      string flowId,
      string completionToken,
      CancellationToken cancellationToken = default)
  {
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      var pending = await ReadUnlockedAsync().ConfigureAwait(false);
      if (pending is not { CompletionToken: null } || pending.FlowId != flowId) return false;
      await SecureStorage.Default.SetAsync(
          StorageKey,
          JsonSerializer.Serialize(pending with { CompletionToken = completionToken })).ConfigureAwait(false);
      return true;
    }
    finally { gate.Release(); }
  }

  public async Task ClearAsync(CancellationToken cancellationToken = default)
  {
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try { SecureStorage.Default.Remove(StorageKey); }
    finally { gate.Release(); }
  }

  public async Task<bool> ClaimFailureAsync(string flowId, CancellationToken cancellationToken = default)
  {
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      var pending = await ReadUnlockedAsync().ConfigureAwait(false);
      if (pending is not { CompletionToken: null } || pending.FlowId != flowId) return false;
      SecureStorage.Default.Remove(StorageKey);
      return true;
    }
    finally { gate.Release(); }
  }

  public void Dispose() => gate.Dispose();

  private static async Task<PendingNativeBlueskyLink?> ReadUnlockedAsync()
  {
    var value = await SecureStorage.Default.GetAsync(StorageKey).ConfigureAwait(false);
    return string.IsNullOrWhiteSpace(value) ? null : JsonSerializer.Deserialize<PendingNativeBlueskyLink>(value);
  }
}
