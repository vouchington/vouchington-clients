using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.HackerNews;
using Xunit;

namespace Voucha.Client.Core.Tests.HackerNews;

public sealed class HackerNewsReferenceGateTests
{
  [Fact]
  public async Task FixtureDecodesAndGateShowsReferences()
  {
    var handler = new HackerNewsConfigHandler(HackerNewsConfigHandler.FixtureOn());
    var client = handler.Client();

    var config = await client.FetchHackerNewsConfigAsync(TestContext.Current.CancellationToken);
    Assert.True(config.ReferencesVisible);
    Assert.Equal("1y", config.PostTimeRangeCap);
    Assert.Equal(HackerNewsConfigHandler.ConfigPath, VouchaApiEndpoints.HackerNewsConfig().Path);

    var gate = new HackerNewsReferenceGate(client.FetchHackerNewsConfigAsync);
    Assert.False(gate.ReferencesVisible);
    var changes = 0;
    gate.Changed += (_, _) => changes++;
    await gate.RefreshAsync(TestContext.Current.CancellationToken);
    await gate.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(gate.ReferencesVisible);
    Assert.Equal(1, changes);
  }

  [Fact]
  public async Task FlagOffStaysHidden()
  {
    var gate = new HackerNewsReferenceGate(
        new HackerNewsConfigHandler(HackerNewsConfigHandler.Off()).Client().FetchHackerNewsConfigAsync);

    await gate.RefreshAsync(TestContext.Current.CancellationToken);

    Assert.False(gate.ReferencesVisible);
  }

  [Fact]
  public async Task FailedFetchStaysHiddenAndKeepsLastKnownValue()
  {
    var fail = false;
    var gate = new HackerNewsReferenceGate(_ => fail
        ? throw new HttpRequestException("offline")
        : Task.FromResult(new HackerNewsConfigResponse(true, "1y")));

    fail = true;
    await gate.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.False(gate.ReferencesVisible);

    fail = false;
    await gate.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(gate.ReferencesVisible);

    fail = true;
    await gate.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(gate.ReferencesVisible);
  }

  [Fact]
  public async Task ServerErrorAndCancellationLeaveTheFilterHidden()
  {
    var handler = new HackerNewsConfigHandler("{}", HttpStatusCode.InternalServerError);
    var gate = new HackerNewsReferenceGate(handler.Client().FetchHackerNewsConfigAsync);
    await gate.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.False(gate.ReferencesVisible);

    var canceled = new HackerNewsReferenceGate(token => Task.FromCanceled<HackerNewsConfigResponse>(token));
    using var source = new CancellationTokenSource();
    await source.CancelAsync();
    await canceled.RefreshAsync(source.Token);
    Assert.False(canceled.ReferencesVisible);
  }

  [Fact]
  public async Task SubscriptionsNotifyUntilDisposedAndDoNotRetainListeners()
  {
    var visible = true;
    var gate = new HackerNewsReferenceGate(_ => Task.FromResult(new HackerNewsConfigResponse(visible, "1y")));
    var listener = new CountingListener();
    var subscription = gate.Subscribe(listener);
    var abandoned = SubscribeAbandonedListener(gate);

    await gate.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.Equal(1, listener.Changes);

    subscription.Dispose();
    subscription.Dispose();
    visible = false;
    await gate.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.Equal(1, listener.Changes);

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    Assert.False(abandoned.TryGetTarget(out _));
    visible = true;
    await gate.RefreshAsync(TestContext.Current.CancellationToken);
    Assert.True(gate.ReferencesVisible);
    Assert.Equal(1, listener.Changes);
    Assert.Throws<ArgumentNullException>(() => gate.Subscribe(null!));
  }

  [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
  private static WeakReference<CountingListener> SubscribeAbandonedListener(HackerNewsReferenceGate gate)
  {
    var listener = new CountingListener();
    _ = gate.Subscribe(listener);
    return new WeakReference<CountingListener>(listener);
  }

  private sealed class CountingListener : IHackerNewsReferenceListener
  {
    public int Changes { get; private set; }

    public void OnHackerNewsReferencesChanged() => Changes++;
  }

  [Fact]
  public void GateRequiresFetchDelegate() =>
      Assert.Throws<ArgumentNullException>(() => new HackerNewsReferenceGate(null!));
}
