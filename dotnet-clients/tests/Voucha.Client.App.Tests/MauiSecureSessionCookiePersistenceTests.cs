using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Storage;
using Voucha.Client.App;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class MauiSecureSessionCookiePersistenceTests
{
  private static readonly Uri Origin = new("https://voucha.test");

  [Fact]
  public async Task AdapterPersistsAndClearsThroughThePlatformStore()
  {
    var storage = new ControlledSecureStorage();
    var adapter = Adapter(storage);

    await adapter.WriteAsync("cookie-snapshot", TestContext.Current.CancellationToken);

    Assert.Equal("cookie-snapshot", await adapter.ReadAsync(TestContext.Current.CancellationToken));
    await adapter.ClearAsync(TestContext.Current.CancellationToken);
    Assert.Null(await adapter.ReadAsync(TestContext.Current.CancellationToken));
    Assert.Empty(storage.StoredValues);
  }

  [Fact]
  public async Task AdapterPropagatesTheOriginalPlatformWriteFailure()
  {
    var storage = new ControlledSecureStorage();
    var adapter = Adapter(storage);
    await adapter.WriteAsync("previous-snapshot", TestContext.Current.CancellationToken);
    storage.WriteFailure = new InvalidOperationException("Platform store unavailable");

    var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        adapter.WriteAsync("replacement-snapshot", TestContext.Current.CancellationToken));

    Assert.Same(storage.WriteFailure, failure);
    Assert.Equal("previous-snapshot", await adapter.ReadAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task RenewalThroughTheRealAdapterFailsAllWaitersAndClearsConsumedCredentials()
  {
    var storage = new ControlledSecureStorage();
    var adapter = Adapter(storage);
    using var jar = new SessionCookieJar(new CookieContainer(), Origin, adapter);
    await jar.StoreSessionTokensAsync("consumed-dt", "consumed-st", TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
    storage.WriteFailure = new InvalidOperationException("Platform store unavailable");
    storage.HoldFailedWrite = true;
    var rotations = 0;
    var invalidations = 0;
    jar.SessionInvalidated += (_, _) => invalidations++;
    Task<NativeSessionRefreshResponse> Rotate(string? dt, string? st, CancellationToken token)
    {
      rotations++;
      Assert.Equal("consumed-dt", dt);
      Assert.Equal("consumed-st", st);
      return Task.FromResult(new NativeSessionRefreshResponse(new NativeRefreshedSession(
          "device", "replacement-dt", 120, true,
          new NativeRefreshedSessionClaims("web", "device", 0, [], 0, "session", "member"),
          "session", "replacement-st", 60, "member")));
    }
    var renewal = jar.RenewAsync(Rotate, cancellationToken: TestContext.Current.CancellationToken);
    Task? coalesced = null;
    Task? ordinaryWaiter = null;
    try
    {
      await storage.WriteEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
      coalesced = jar.RenewAsync(Rotate, cancellationToken: TestContext.Current.CancellationToken);
      ordinaryWaiter = jar.WaitForRenewalAsync(TestContext.Current.CancellationToken);
      Assert.False(renewal.IsCompleted);
      Assert.False(coalesced.IsCompleted);
      Assert.False(ordinaryWaiter.IsCompleted);
      Assert.Contains("consumed-st", Assert.Single(storage.StoredValues));
    }
    finally { storage.ReleaseWrite.TrySetResult(); }

    Assert.Same(storage.WriteFailure, await Assert.ThrowsAsync<InvalidOperationException>(() => renewal));
    Assert.Same(storage.WriteFailure, await Assert.ThrowsAsync<InvalidOperationException>(() => coalesced!));
    Assert.Same(storage.WriteFailure, await Assert.ThrowsAsync<InvalidOperationException>(() => ordinaryWaiter!));
    Assert.Equal(1, rotations);
    Assert.Equal(1, invalidations);
    Assert.False(jar.HasDeviceCookie);
    Assert.False(jar.CanRecoverAuthentication);
    Assert.Empty(jar.GetCookieHeader(Origin));
    Assert.Empty(storage.StoredValues);
    using var restarted = new SessionCookieJar(new CookieContainer(), Origin, Adapter(storage));
    await restarted.RestoreAsync(TestContext.Current.CancellationToken);
    Assert.False(restarted.HasDeviceCookie);
    Assert.Empty(restarted.GetCookieHeader(Origin));
  }

  private static MauiSecureSessionCookiePersistence Adapter(ISecureStorage storage) =>
      new(Origin, NullLogger<MauiSecureSessionCookiePersistence>.Instance, storage);

  private sealed class ControlledSecureStorage : ISecureStorage
  {
    private readonly object gate = new();
    private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    public Exception? WriteFailure { get; set; }
    public bool HoldFailedWrite { get; set; }
    public TaskCompletionSource WriteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string[] StoredValues { get { lock (gate) return values.Values.ToArray(); } }
    public Task<string?> GetAsync(string key)
    {
      lock (gate) return Task.FromResult(values.GetValueOrDefault(key));
    }
    public async Task SetAsync(string key, string value)
    {
      if (WriteFailure is { } failure)
      {
        WriteEntered.TrySetResult();
        if (HoldFailedWrite) await ReleaseWrite.Task;
        throw failure;
      }
      lock (gate) values[key] = value;
    }
    public bool Remove(string key) { lock (gate) return values.Remove(key); }
    public void RemoveAll() { lock (gate) values.Clear(); }
  }
}
