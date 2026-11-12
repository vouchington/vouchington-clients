using Voucha.Client.Core.Analytics;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tests.LandingPages;
using Xunit;

namespace Voucha.Client.Core.Tests.Analytics;

public sealed class SessionProductSurfaceAnalyticsBoundsTests
{
  [Fact]
  public async Task AtMostFourInFlightSignalsDropOverloadWithoutBacklogAndReleaseFailedSlots()
  {
    var handler = new PendingHandler();
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    var analytics = new SessionProductSurfaceAnalytics(new VouchaApiClient(http), new PublicLandingSession());
    var tasks = new List<Task>();
    for (var index = 0; index < 4; index++)
    {
      analytics.RecordLandingPageClick("page", new("item"));
      tasks.Add(analytics.LastDispatch);
    }
    for (var index = 0; index < 20; index++) analytics.RecordLandingPageVisit("page", new());
    Assert.Equal(4, handler.Started);
    Assert.All(tasks, task => Assert.False(task.IsCompleted));
    handler.Release.SetResult();
    await Task.WhenAll(tasks).WaitAsync(TestContext.Current.CancellationToken);
    Assert.Equal(4, handler.Started);
    analytics.RecordLandingPageClick("page", new("next-item"));
    await analytics.LastDispatch.WaitAsync(TestContext.Current.CancellationToken);
    Assert.Equal(5, handler.Started);
  }

  [Fact]
  public async Task AccountChangeCancelsInFlightSignalAndSignedOutDoesNotReplaceIt()
  {
    var handler = new PendingHandler();
    using var http = new HttpClient(handler) { BaseAddress = new("https://api.test") };
    var session = new PublicLandingSession();
    var analytics = new SessionProductSurfaceAnalytics(new VouchaApiClient(http), session);
    analytics.RecordLandingPageVisit("page", new());
    var dispatch = analytics.LastDispatch;
    session.Set(null);
    await dispatch.WaitAsync(TestContext.Current.CancellationToken);
    Assert.True(handler.CancellationObserved);
    analytics.RecordLandingPageClick("page", new("item"));
    Assert.Equal(1, handler.Started);
    session.Set("next-account");
    analytics.RecordLandingPageVisit("page", new());
    handler.Release.SetResult();
    await analytics.LastDispatch.WaitAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, handler.Started);
  }

  private sealed class PendingHandler : HttpMessageHandler
  {
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Started { get; private set; }
    public bool CancellationObserved { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Started++;
      try { await Release.Task.WaitAsync(cancellationToken); }
      catch (OperationCanceledException) { CancellationObserved = true; throw; }
      throw new HttpRequestException("dropped failure");
    }
  }
}
