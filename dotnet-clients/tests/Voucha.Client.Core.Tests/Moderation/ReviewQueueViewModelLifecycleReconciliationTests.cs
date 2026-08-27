using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ReviewQueueViewModelLifecycleReconciliationTests
{
  [Fact]
  public async Task ReloadWaitsForCanceledMutationThenReplacesWithAuthoritativeRows()
  {
    var service = new LifecycleReconciliationService();
    var viewModel = new ReviewQueueViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var row = viewModel.Items[0];
    using var oldPage = new CancellationTokenSource();

    var staleRefresh = viewModel.RefreshAsync(oldPage.Token);
    await service.StaleFetchStarted.Task;
    viewModel.CancelListOperations();
    var mutation = viewModel.PerformAsync(row, PostClearanceAction.Approved, oldPage.Token);
    await service.MutationStarted.Task;

    oldPage.Cancel();
    Assert.True(viewModel.RequiresReconciliation);
    Assert.False(viewModel.CanPerform(row, PostClearanceAction.Approved));

    var reload = viewModel.ReloadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, service.FetchCount);

    service.ReleaseMutation();
    await mutation;
    await reload;
    Assert.Equal(3, service.FetchCount);
    Assert.Empty(viewModel.Items);
    Assert.False(viewModel.RequiresReconciliation);

    service.ReleaseStaleFetch();
    await staleRefresh;
    Assert.Empty(viewModel.Items);
  }

  [Fact]
  public async Task CleanReloadRefreshesAnAlreadyLoadedQueue()
  {
    var service = new CleanReloadService();
    var viewModel = new ReviewQueueViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.ReloadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, service.FetchCount);
    Assert.Empty(viewModel.Items);
    Assert.False(viewModel.RequiresReconciliation);
  }

  [Fact]
  public async Task FailedReconciliationReplacementRemainsRetryable()
  {
    var service = new LifecycleReconciliationService(failFirstReconciliation: true);
    var viewModel = new ReviewQueueViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    using var oldPage = new CancellationTokenSource();

    var mutation = viewModel.PerformAsync(viewModel.Items[0], PostClearanceAction.Approved, oldPage.Token);
    await service.MutationStarted.Task;
    oldPage.Cancel();
    service.ReleaseMutation();
    await mutation;

    await viewModel.ResumeAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.RequiresReconciliation);
    Assert.True(viewModel.HasError);

    await viewModel.ResumeAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.RequiresReconciliation);
    Assert.Empty(viewModel.Items);
  }

  private sealed class CleanReloadService : ReviewQueueModerationServiceStub
  {
    public int FetchCount { get; private set; }

    public override Task<AdminReviewQueueResponse> FetchReviewQueueAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
      FetchCount++;
      return Task.FromResult(
          FetchCount == 1
              ? Decode<AdminReviewQueueResponse>("native.moderation.review-queue.default")
              : new AdminReviewQueueResponse([], new PageInfo(null, false, null)));
    }
  }

  private sealed class LifecycleReconciliationService(bool failFirstReconciliation = false)
      : ReviewQueueModerationServiceStub
  {
    private readonly TaskCompletionSource mutationRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource staleFetchRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AdminReviewQueueResponse initial = Decode<AdminReviewQueueResponse>(
        "native.moderation.review-queue.default");
    private int reconciliationAttempts;
    public int FetchCount { get; private set; }
    public TaskCompletionSource MutationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource StaleFetchStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void ReleaseMutation() => mutationRelease.TrySetResult();
    public void ReleaseStaleFetch() => staleFetchRelease.TrySetResult();

    public override async Task<AdminReviewQueueResponse> FetchReviewQueueAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
      FetchCount++;
      if (FetchCount == 1) return initial;
      if (FetchCount == 2 && !failFirstReconciliation)
      {
        StaleFetchStarted.TrySetResult();
        await staleFetchRelease.Task;
        return initial;
      }

      reconciliationAttempts++;
      if (failFirstReconciliation && reconciliationAttempts == 1) throw new HttpRequestException("temporary");
      return new AdminReviewQueueResponse([], new PageInfo(null, false, null));
    }

    public override async Task<ClearanceUpdateResponse> UpdatePostClearanceAsync(
        string postId,
        PostClearanceAction status,
        CancellationToken cancellationToken = default)
    {
      MutationStarted.TrySetResult();
      await mutationRelease.Task;
      return Decode<ClearanceUpdateResponse>("native.moderation.clearance.approved");
    }
  }

  private static T Decode<T>(string fixtureId) =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options) ??
      throw new InvalidOperationException($"{fixtureId} did not decode.");
}
