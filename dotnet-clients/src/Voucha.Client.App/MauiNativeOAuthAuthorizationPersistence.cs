using System.Text.Json;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

public sealed class MauiNativeOAuthAuthorizationPersistence :
    INativeOAuthAuthorizationPersistence,
    IDisposable
{
  private const string StorageKey = "native-oauth-authorization-v1";
  private readonly SemaphoreSlim gate = new(1, 1);

  public Task<NativeOAuthAuthorizationSnapshot> ReadAsync(
      CancellationToken cancellationToken = default) =>
      ReadAsync(
          snapshot => new NativeOAuthAuthorizationSnapshot(snapshot?.Pending, snapshot?.Result),
          cancellationToken);

  public Task WritePendingAsync(
      PendingNativeOAuthAuthorization pending,
      CancellationToken cancellationToken = default) =>
      MutateAsync(_ => new(pending, null), cancellationToken);

  public async Task<bool> ClaimCallbackAsync(
      string flowId,
      string completionToken,
      CancellationToken cancellationToken = default)
  {
    var claimed = false;
    await MutateAsync(snapshot =>
    {
      if (snapshot?.Pending is not { CompletionToken: null } pending ||
          pending.FlowId != flowId)
      {
        return snapshot;
      }
      claimed = true;
      return snapshot with
      {
        Pending = pending with { CompletionToken = completionToken },
      };
    }, cancellationToken).ConfigureAwait(false);
    return claimed;
  }

  public async Task<bool> CompleteAsync(
      string flowId,
      NativeOAuthAuthorizationResult result,
      CancellationToken cancellationToken = default)
  {
    var completed = false;
    await MutateAsync(snapshot =>
    {
      if (snapshot?.Pending?.FlowId != flowId) return snapshot;
      completed = true;
      return new(null, result);
    }, cancellationToken).ConfigureAwait(false);
    return completed;
  }

  public Task ClearPendingAsync(CancellationToken cancellationToken = default) =>
      MutateAsync(
          snapshot => snapshot is null ? null : snapshot with { Pending = null },
          cancellationToken);

  public Task AcknowledgeResultAsync(CancellationToken cancellationToken = default) =>
      MutateAsync(
          snapshot => snapshot is null ? null : snapshot with { Result = null },
          cancellationToken);

  public void Dispose() => gate.Dispose();

  private async Task<T?> ReadAsync<T>(
      Func<StoredOAuthAuthorization?, T?> select,
      CancellationToken cancellationToken)
  {
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      return select(await ReadUnlockedAsync().ConfigureAwait(false));
    }
    finally
    {
      gate.Release();
    }
  }

  private async Task MutateAsync(
      Func<StoredOAuthAuthorization?, StoredOAuthAuthorization?> mutation,
      CancellationToken cancellationToken)
  {
    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      var next = mutation(await ReadUnlockedAsync().ConfigureAwait(false));
      if (next is null || next is { Pending: null, Result: null })
      {
        SecureStorage.Default.Remove(StorageKey);
      }
      else
      {
        await SecureStorage.Default.SetAsync(StorageKey, JsonSerializer.Serialize(next))
            .ConfigureAwait(false);
      }
    }
    finally
    {
      gate.Release();
    }
  }

  private static async Task<StoredOAuthAuthorization?> ReadUnlockedAsync()
  {
    var value = await SecureStorage.Default.GetAsync(StorageKey).ConfigureAwait(false);
    if (string.IsNullOrWhiteSpace(value)) return null;
    try
    {
      return JsonSerializer.Deserialize<StoredOAuthAuthorization>(value);
    }
    catch (JsonException)
    {
      SecureStorage.Default.Remove(StorageKey);
      return null;
    }
  }

  private sealed record StoredOAuthAuthorization(
      PendingNativeOAuthAuthorization? Pending,
      NativeOAuthAuthorizationResult? Result);
}
