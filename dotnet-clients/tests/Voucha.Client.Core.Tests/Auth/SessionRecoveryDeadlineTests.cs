using System.Net;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed partial class SessionRecoveryTests
{
  [Fact]
  public async Task SharedRenewalDeadlineOutlivesCanceledWaiterAndReleasesLaterRequests()
  {
    var clock = new RenewalReviewClock();
    using var fixture = new RecoveryFixture(timeProvider: clock);
    await fixture.SeedAsync();
    using var waiter = new CancellationTokenSource();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    CancellationToken transportToken = default;
    fixture.Terminal.RenewWithCancellation = async (_, token) =>
    {
      transportToken = token;
      entered.TrySetResult();
      await Task.Delay(Timeout.InfiniteTimeSpan, token);
      return Rotated();
    };
    var canceled = fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"), waiter.Token);
    await Task.WhenAny(entered.Task, canceled).WaitAsync(TestContext.Current.CancellationToken);
    if (canceled.IsCompleted) await canceled;
    await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
    var survivor = ((IConfirmingSessionRefreshStore)fixture.Store)
        .RefreshConfirmingAsync(TestContext.Current.CancellationToken);
    try
    {
      waiter.Cancel();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
      Assert.False(transportToken.IsCancellationRequested);
      clock.Advance(TimeSpan.FromSeconds(29));
      Assert.False(survivor.IsCompleted);
      clock.Advance(TimeSpan.FromSeconds(1));
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => survivor);
    }
    finally { clock.Advance(TimeSpan.FromSeconds(30)); }
    Assert.True(transportToken.IsCancellationRequested);
    Assert.Equal(1, fixture.Terminal.RenewCount);
    Assert.Contains("old-st", fixture.Persistence.Value!);
    fixture.Terminal.RenewWithCancellation = (_, _) => Task.FromResult(Rotated());
    await fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken);
    Assert.Equal(2, fixture.Terminal.RenewCount);
    Assert.Contains("new-st", fixture.Persistence.Value!);
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task SharedRenewalDeadlineCancelsSuccessAndErrorBodyReads(bool lifecycle, bool errorBody)
  {
    var clock = new RenewalReviewClock();
    using var fixture = new RecoveryFixture(timeProvider: clock);
    await fixture.SeedAsync();
    var content = new DeadlineReviewContent();
    CancellationToken transportToken = default;
    fixture.Terminal.RenewWithCancellation = (_, token) =>
    {
      transportToken = token;
      return Task.FromResult(new HttpResponseMessage(errorBody ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
      {
        Content = content,
      });
    };
    var request = lifecycle
        ? ((IConfirmingSessionRefreshStore)fixture.Store).RefreshConfirmingAsync(TestContext.Current.CancellationToken)
        : fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken);
    try
    {
      await Task.WhenAny(content.Entered.Task, request).WaitAsync(TestContext.Current.CancellationToken);
      if (request.IsCompleted) await request;
      await content.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
      Assert.False(content.ReadToken.IsCancellationRequested);
      clock.Advance(TimeSpan.FromSeconds(30));
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }
    finally { clock.Advance(TimeSpan.FromSeconds(30)); }
    Assert.True(transportToken.CanBeCanceled);
    if (!lifecycle) Assert.True(transportToken.IsCancellationRequested);
    Assert.True(content.ReadToken.IsCancellationRequested);
    Assert.True(fixture.Jar.HasDeviceCookie);
    Assert.Contains("old-st", fixture.Persistence.Value!);
    fixture.Terminal.RenewWithCancellation = (_, _) => Task.FromResult(Rotated());
    await fixture.Client.SendAsync(new ApiRequest(HttpMethod.Get, "/api/v1/posts"), TestContext.Current.CancellationToken);
    Assert.Equal(2, fixture.Terminal.RenewCount);
    Assert.Contains("new-st", fixture.Persistence.Value!);
  }

  private sealed class DeadlineReviewContent : HttpContent
  {
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken ReadToken { get; private set; }
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();
    protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new DeadlineReviewStream(this));
    private sealed class DeadlineReviewStream(DeadlineReviewContent owner) : MemoryStream
    {
      public override bool CanSeek => false;
      public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
      {
        owner.ReadToken = cancellationToken;
        owner.Entered.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return 0;
      }
    }
  }

  private sealed class RenewalReviewClock : TimeProvider
  {
    private DateTimeOffset now = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);
    private readonly List<ReviewTimer> timers = [];
    public override DateTimeOffset GetUtcNow() => now;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
      var timer = new ReviewTimer(this, callback, state);
      timer.Change(dueTime, period);
      timers.Add(timer);
      return timer;
    }
    public void Advance(TimeSpan elapsed)
    {
      now += elapsed;
      foreach (var timer in timers.ToArray()) timer.Fire();
    }
    private sealed class ReviewTimer(RenewalReviewClock clock, TimerCallback callback, object? state) : ITimer
    {
      private bool disposed;
      private DateTimeOffset? due;
      public bool Change(TimeSpan dueTime, TimeSpan period)
      {
        if (disposed) return false;
        due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.now.Add(dueTime);
        return true;
      }
      public void Fire()
      {
        if (disposed || due is null || due > clock.now) return;
        due = null;
        callback(state);
      }
      public void Dispose() => disposed = true;
      public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
  }
}
