using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ReviewQueueExposureViewModelTests
{
  [Fact]
  public async Task InitialLoadHydratesExposureAndSensitiveMedia()
  {
    var exposure = new ExposureService(Exposure());
    var viewModel = Create(exposure, Post("sensitive", true, Image("image-1")));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Items);
    Assert.Equal(1, exposure.FetchCount);
    Assert.True(row.RequiresMediaReveal);
    Assert.False(row.ShowMedia);
    Assert.True(row.CanRevealMedia);
  }

  [Fact]
  public async Task RevealIsOptimisticAndSubmittedExactlyOnce()
  {
    var exposure = new ExposureService(Exposure()) { HoldReveal = true };
    var viewModel = Create(exposure, Post("sensitive", true, Image("image-1"), Image("image-2")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var first = viewModel.RevealMediaAsync(viewModel.Items[0], TestContext.Current.CancellationToken);
    await exposure.RevealStarted.Task;
    var duplicate = viewModel.RevealMediaAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    var revealed = Assert.Single(viewModel.Items);
    Assert.True(revealed.ShowMedia);
    Assert.True(revealed.IsRevealInFlight);
    Assert.Equal(2, revealed.Media.Count);
    Assert.Equal(["sensitive"], exposure.RevealedPostIds);

    exposure.ReleaseReveal();
    await Task.WhenAll(first, duplicate);
    Assert.False(Assert.Single(viewModel.Items).IsRevealInFlight);
    Assert.Single(exposure.RevealedPostIds);
  }

  [Fact]
  public async Task AmbiguousFailureKeepsMediaVisibleAndGatesOtherReveals()
  {
    var exposure = new ExposureService(Exposure()) { RevealFailure = new HttpRequestException("lost") };
    var viewModel = Create(
        exposure,
        Post("first", true, Image("image-1")),
        Post("second", true, Image("image-2")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.RevealMediaAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    var first = viewModel.Items.Single(row => row.Id == "first");
    var second = viewModel.Items.Single(row => row.Id == "second");
    Assert.True(first.ShowMedia);
    Assert.True(first.IsExposureStale);
    Assert.False(second.CanRevealMedia);
    await viewModel.RevealMediaAsync(second, TestContext.Current.CancellationToken);
    Assert.Equal(["first"], exposure.RevealedPostIds);
  }

  [Fact]
  public async Task ExplicitRefreshRecoversStaleExposure()
  {
    var exposure = new ExposureService(Exposure(), Exposure())
    {
      RevealFailure = new HttpRequestException("lost"),
    };
    var viewModel = Create(
        exposure,
        Post("first", true, Image("image-1")),
        Post("second", true, Image("image-2")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.RevealMediaAsync(viewModel.Items[0], TestContext.Current.CancellationToken);

    await viewModel.RefreshExposureAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, exposure.FetchCount);
    Assert.False(viewModel.Items.Single(row => row.Id == "second").IsExposureStale);
    Assert.True(viewModel.Items.Single(row => row.Id == "second").CanRevealMedia);
  }

  [Fact]
  public async Task CooldownOnlyGatesRevealAndExpiryRefetchesOnce()
  {
    var delay = new ControlledDelay();
    var exposure = new ExposureService(
        Exposure(inCooldown: true, cooldownEndsAt: DateTimeOffset.UnixEpoch.AddMinutes(1)),
        Exposure());
    var viewModel = Create(
        exposure,
        delay: delay.Invoke,
        Post("sensitive", true, Image("image-1")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await delay.Started.Task;

    var coolingDown = Assert.Single(viewModel.Items);
    Assert.True(coolingDown.IsInExposureCooldown);
    Assert.False(coolingDown.CanRevealMedia);
    Assert.True(coolingDown.CanAct);

    delay.Release();
    await WaitUntilAsync(() => exposure.FetchCount == 2);
    await WaitUntilAsync(() => Assert.Single(viewModel.Items).CanRevealMedia);
    Assert.Equal(1, delay.CallCount);
  }

  [Fact]
  public async Task FailedExpiryRefetchStaysStaleWithoutLooping()
  {
    var delay = new ControlledDelay();
    var exposure = new ExposureService(
        Exposure(inCooldown: true, cooldownEndsAt: DateTimeOffset.UnixEpoch.AddMinutes(1)),
        new HttpRequestException("offline"));
    var viewModel = Create(
        exposure,
        delay: delay.Invoke,
        Post("sensitive", true, Image("image-1")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await delay.Started.Task;

    delay.Release();
    await WaitUntilAsync(() => exposure.FetchCount == 2);
    await WaitUntilAsync(() => Assert.Single(viewModel.Items).IsExposureStale);
    await Task.Yield();

    Assert.Equal(2, exposure.FetchCount);
    Assert.Equal(1, delay.CallCount);
    Assert.False(Assert.Single(viewModel.Items).CanRevealMedia);
  }

  [Fact]
  public async Task NonSensitiveMediaRemainsVisibleWhileExposureIsStale()
  {
    var exposure = new ExposureService(new HttpRequestException("offline"));
    var viewModel = Create(exposure, Post("safe", false, Image("image-1")));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var row = Assert.Single(viewModel.Items);
    Assert.True(row.IsExposureStale);
    Assert.True(row.ShowMedia);
    Assert.False(row.RequiresMediaReveal);
  }

  [Fact]
  public async Task OlderExposureGetCannotOverwriteRevealWriterState()
  {
    var posts = new[]
    {
      Post("first", true, Image("image-1")),
      Post("second", true, Image("image-2")),
    };
    var exposure = new RacingExposureService();
    var viewModel = new ReviewQueueViewModel(
        new QueueService(posts),
        exposure,
        new AppConfig(new Uri("https://api.test")),
        localization: null,
        localeController: null,
        utcNow: () => DateTimeOffset.UnixEpoch,
        delay: static (_, _) => Task.CompletedTask);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var reveal = viewModel.RevealMediaAsync(
        viewModel.Items.Single(row => row.Id == "first"),
        TestContext.Current.CancellationToken);
    await exposure.RevealStarted.Task;
    var refresh = viewModel.RefreshAsync(TestContext.Current.CancellationToken);
    await exposure.OlderGetStarted.Task;

    exposure.CompleteRevealInCooldown();
    await reveal;
    Assert.True(viewModel.Items.Single(row => row.Id == "second").IsInExposureCooldown);

    exposure.CompleteOlderGetWithoutCooldown();
    await refresh;

    var second = viewModel.Items.Single(row => row.Id == "second");
    Assert.True(second.IsInExposureCooldown);
    Assert.False(second.CanRevealMedia);
  }

  [Fact]
  public async Task CanceledLifecycleRejectsLateRevealAndResumeHydratesCooldown()
  {
    var delay = new ControlledDelay();
    var exposure = new ExposureService(
        Exposure(),
        Exposure(inCooldown: true, cooldownEndsAt: DateTimeOffset.UnixEpoch.AddMinutes(1)))
    {
      HoldReveal = true,
      IgnoreRevealCancellation = true,
      RevealFailure = new HttpRequestException("late failure"),
    };
    var viewModel = Create(
        exposure,
        delay: delay.Invoke,
        Post("first", true, Image("image-1")),
        Post("second", true, Image("image-2")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    using var oldPage = new CancellationTokenSource();

    var reveal = viewModel.RevealMediaAsync(viewModel.Items[0], oldPage.Token);
    await exposure.RevealStarted.Task;
    viewModel.CancelExposureOperations();
    oldPage.Cancel();
    exposure.ReleaseReveal();
    await reveal;

    Assert.True(viewModel.Items.Single(row => row.Id == "second").IsExposureStale);
    await viewModel.ResumeAsync(TestContext.Current.CancellationToken);

    var second = viewModel.Items.Single(row => row.Id == "second");
    Assert.Equal(2, exposure.FetchCount);
    Assert.True(second.IsInExposureCooldown);
    Assert.False(second.CanRevealMedia);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task LateCanceledRevealDoesNotClobberCompletedResumeRefresh(
      bool failReveal)
  {
    var delay = new ControlledDelay();
    var exposure = new ExposureService(
        Exposure(),
        Exposure(inCooldown: true, cooldownEndsAt: DateTimeOffset.UnixEpoch.AddMinutes(1)),
        Exposure())
    {
      HoldReveal = true,
      IgnoreRevealCancellation = true,
      RevealFailure = failReveal ? new HttpRequestException("late failure") : null,
    };
    var viewModel = Create(
        exposure,
        delay: delay.Invoke,
        Post("first", true, Image("image-1")),
        Post("second", true, Image("image-2")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    using var oldPage = new CancellationTokenSource();

    var reveal = viewModel.RevealMediaAsync(viewModel.Items[0], oldPage.Token);
    await exposure.RevealStarted.Task;
    viewModel.CancelExposureOperations();
    oldPage.Cancel();
    await viewModel.ResumeAsync(TestContext.Current.CancellationToken);
    await delay.Started.Task;
    Assert.True(viewModel.Items.Single(row => row.Id == "second").IsInExposureCooldown);

    exposure.ReleaseReveal();
    await reveal;

    var second = viewModel.Items.Single(row => row.Id == "second");
    Assert.False(second.IsExposureStale);
    Assert.True(second.IsInExposureCooldown);
    Assert.False(second.CanRevealMedia);

    delay.Release();
    await WaitUntilAsync(() => exposure.FetchCount == 3);
    await WaitUntilAsync(() => viewModel.Items.Single(row => row.Id == "second").CanRevealMedia);
  }

  [Fact]
  public async Task ExposureGetFinishingAfterPageDisappearsStaysStaleUntilResume()
  {
    var exposure = new PageLifecycleExposureService();
    var viewModel = new ReviewQueueViewModel(
        new QueueService([Post("sensitive", true, Image("image-1"))]),
        exposure,
        new AppConfig(new Uri("https://api.test")));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var refresh = viewModel.RefreshExposureAsync(TestContext.Current.CancellationToken);
    await exposure.StaleGetStarted.Task;
    viewModel.CancelExposureOperations();
    exposure.CompleteStaleGet();
    await refresh;

    Assert.True(Assert.Single(viewModel.Items).IsExposureStale);
    await viewModel.ResumeAsync(TestContext.Current.CancellationToken);
    var resumed = Assert.Single(viewModel.Items);
    Assert.Equal(3, exposure.FetchCount);
    Assert.False(resumed.IsExposureStale);
    Assert.True(resumed.IsInExposureCooldown);
    Assert.False(resumed.CanRevealMedia);
  }

  private static ReviewQueueViewModel Create(
      ExposureService exposure,
      params AdminReviewQueuePost[] posts) =>
      Create(exposure, static (_, _) => Task.CompletedTask, posts);

  private static ReviewQueueViewModel Create(
      ExposureService exposure,
      Func<TimeSpan, CancellationToken, Task> delay,
      params AdminReviewQueuePost[] posts) =>
      new(
          new QueueService(posts),
          exposure,
          new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")),
          localization: null,
          localeController: null,
          utcNow: () => DateTimeOffset.UnixEpoch,
          delay: delay);

  private static ModerationExposureState Exposure(
      bool inCooldown = false,
      DateTimeOffset? cooldownEndsAt = null) =>
      new(1, 10, inCooldown, cooldownEndsAt);

  private static AdminReviewQueueImage Image(string id) => new(id, 0, $"Caption {id}");

  private static AdminReviewQueuePost Post(
      string id,
      bool requiresReveal,
      params AdminReviewQueueImage[] images) =>
      new(
          id, id, id, "Preview", "discussion", "author", DateTimeOffset.UnixEpoch,
          null, null, null, AdminReviewQueueClearanceStatus.Rejected, null,
          false, 0, EmptyJson(), false, EmptyJson(), new(requiresReveal, images));

  private static JsonElement EmptyJson() => JsonDocument.Parse("{}").RootElement.Clone();

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(10);
    Assert.True(condition());
  }

  private sealed class QueueService(IReadOnlyList<AdminReviewQueuePost> posts)
      : ReviewQueueModerationServiceStub
  {
    public override Task<AdminReviewQueueResponse> FetchReviewQueueAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AdminReviewQueueResponse(posts, new PageInfo(null, false, null)));
  }

  private sealed class ExposureService(params object[] fetchResults) : IModerationExposureService
  {
    private readonly Queue<object> results = new(fetchResults);
    private readonly TaskCompletionSource revealRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int FetchCount { get; private set; }
    public List<string> RevealedPostIds { get; } = [];
    public bool HoldReveal { get; init; }
    public bool IgnoreRevealCancellation { get; init; }
    public Exception? RevealFailure { get; init; }
    public TaskCompletionSource RevealStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ModerationExposureResponse> FetchExposureAsync(
        CancellationToken cancellationToken = default)
    {
      FetchCount++;
      var result = results.Dequeue();
      return result is Exception failure
          ? Task.FromException<ModerationExposureResponse>(failure)
          : Task.FromResult(new ModerationExposureResponse((ModerationExposureState)result));
    }

    public async Task<ModerationExposureResponse> RecordReviewQueueRevealAsync(
        string postId,
        CancellationToken cancellationToken = default)
    {
      RevealedPostIds.Add(postId);
      RevealStarted.TrySetResult();
      if (HoldReveal)
      {
        if (IgnoreRevealCancellation)
          await revealRelease.Task;
        else
          await revealRelease.Task.WaitAsync(cancellationToken);
      }
      if (RevealFailure is not null) throw RevealFailure;
      return new ModerationExposureResponse(Exposure());
    }

    public void ReleaseReveal() => revealRelease.TrySetResult();
  }

  private sealed class RacingExposureService : IModerationExposureService
  {
    private readonly TaskCompletionSource<ModerationExposureResponse> olderGet =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<ModerationExposureResponse> reveal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int fetchCount;
    public TaskCompletionSource OlderGetStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource RevealStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ModerationExposureResponse> FetchExposureAsync(
        CancellationToken cancellationToken = default)
    {
      if (Interlocked.Increment(ref fetchCount) == 1)
      {
        return Task.FromResult(new ModerationExposureResponse(Exposure()));
      }
      OlderGetStarted.TrySetResult();
      return olderGet.Task.WaitAsync(cancellationToken);
    }

    public Task<ModerationExposureResponse> RecordReviewQueueRevealAsync(
        string postId,
        CancellationToken cancellationToken = default)
    {
      RevealStarted.TrySetResult();
      return reveal.Task.WaitAsync(cancellationToken);
    }

    public void CompleteRevealInCooldown() =>
        reveal.TrySetResult(new ModerationExposureResponse(
            Exposure(inCooldown: true)));

    public void CompleteOlderGetWithoutCooldown() =>
        olderGet.TrySetResult(new ModerationExposureResponse(Exposure()));
  }

  private sealed class PageLifecycleExposureService : IModerationExposureService
  {
    private readonly TaskCompletionSource<ModerationExposureResponse> staleGet =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int FetchCount { get; private set; }
    public TaskCompletionSource StaleGetStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ModerationExposureResponse> FetchExposureAsync(
        CancellationToken cancellationToken = default)
    {
      FetchCount++;
      if (FetchCount == 2)
      {
        StaleGetStarted.TrySetResult();
        return staleGet.Task;
      }
      return Task.FromResult(new ModerationExposureResponse(
          Exposure(inCooldown: FetchCount == 3)));
    }

    public Task<ModerationExposureResponse> RecordReviewQueueRevealAsync(
        string postId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModerationExposureResponse(Exposure()));

    public void CompleteStaleGet() =>
        staleGet.TrySetResult(new ModerationExposureResponse(Exposure()));
  }

  private sealed class ControlledDelay
  {
    private readonly TaskCompletionSource release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int CallCount { get; private set; }
    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task Invoke(TimeSpan delay, CancellationToken cancellationToken)
    {
      Assert.True(delay > TimeSpan.Zero);
      CallCount++;
      Started.TrySetResult();
      await release.Task.WaitAsync(cancellationToken);
    }

    public void Release() => release.TrySetResult();
  }
}
