using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.RewardsProgramStatuses;
using Xunit;

namespace Voucha.Client.Core.Tests.RewardsProgramStatuses;

public sealed class RewardsProgramStatusesViewModelLoadingTests
{
  [Fact]
  public async Task LoadsEmptyAndPartialFirstPages()
  {
    var service = new RewardsProgramStatusesTestService();
    service.Pages.Enqueue(RewardsProgramStatusesTestService.Page(false, null));
    var empty = new RewardsProgramStatusesViewModel(service);
    await empty.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(empty.ShowsEmptyState);

    service.Pages.Enqueue(RewardsProgramStatusesTestService.Page(true, "cursor-2", RewardsProgramStatusesTestService.Status("a")));
    var partial = new RewardsProgramStatusesViewModel(service);
    await partial.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["a"], partial.Statuses.Select(value => value.Id));
    Assert.True(partial.HasNextPage);
  }

  [Fact]
  public void LegacyMissingPageInfoDecodesToAnExhaustedPage()
  {
    const string json = """{"results":[{"id":"a","rewards_program_status_id":"topic-a","rewards_program_status":{"id":"topic-a","name":"Gold","slug":"gold"},"since":null,"until":null}]}""";
    var response = JsonSerializer.Deserialize<RewardsProgramStatusesResponse>(json, VouchaApiJson.Options)!;
    var page = new RewardsProgramStatusPage(response.Results.Select(RewardsProgramStatus.FromWire).ToArray(), response.PageInfo ?? new(null, false, null));
    Assert.False(page.PageInfo.HasNextPage);
    Assert.Null(page.PageInfo.EndCursor);
    Assert.Equal("Gold", page.Results.Single().RewardsProgram.Name);
  }

  [Fact]
  public async Task StaleContinuationDoesNotLeaveLoadingMoreSet()
  {
    var service = new RewardsProgramStatusesTestService();
    service.Pages.Enqueue(RewardsProgramStatusesTestService.Page(true, "next", RewardsProgramStatusesTestService.Status("a")));
    var model = new RewardsProgramStatusesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var pending = new TaskCompletionSource<RewardsProgramStatusPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.FetchPending = pending.Task;
    var continuation = model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.True(model.IsLoadingMore);
    service.Pages.Enqueue(RewardsProgramStatusesTestService.Page(false, null, RewardsProgramStatusesTestService.Status("fresh")));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    pending.SetResult(RewardsProgramStatusesTestService.Page(false, null, RewardsProgramStatusesTestService.Status("stale")));
    await continuation;

    Assert.False(model.IsLoadingMore);
    Assert.Equal(["fresh"], model.Statuses.Select(value => value.Id));
  }

  [Fact]
  public async Task ContinuationFailurePreservesRowsAndRetryUsesTheSameCursor()
  {
    var service = new RewardsProgramStatusesTestService();
    service.Pages.Enqueue(RewardsProgramStatusesTestService.Page(true, "next", RewardsProgramStatusesTestService.Status("a")));
    var model = new RewardsProgramStatusesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.FetchErrors.Enqueue(new InvalidOperationException("offline"));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasContinuationError);
    Assert.Equal(["a"], model.Statuses.Select(value => value.Id));
    service.Pages.Enqueue(RewardsProgramStatusesTestService.Page(false, null, RewardsProgramStatusesTestService.Status("b")));
    await model.RetryAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["next", "next"], service.Cursors.Skip(1));
    Assert.Equal(["a", "b"], model.Statuses.Select(value => value.Id));
  }

  [Fact]
  public async Task CancellationClearsLoadingWithoutDiscardingRows()
  {
    var service = new RewardsProgramStatusesTestService();
    service.Pages.Enqueue(RewardsProgramStatusesTestService.Page(false, null, RewardsProgramStatusesTestService.Status("a")));
    var model = new RewardsProgramStatusesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    service.FetchPending = Task.FromCanceled<RewardsProgramStatusPage>(cancellation.Token);
    await model.LoadAsync(cancellation.Token);
    Assert.False(model.IsLoading);
    Assert.Equal(["a"], model.Statuses.Select(value => value.Id));
  }

  [Fact]
  public async Task StaleSearchIsIsolatedAndDuplicateOptionsAreRemoved()
  {
    var service = new RewardsProgramStatusesTestService();
    var stale = new TaskCompletionSource<IReadOnlyList<RewardsProgramStatusOption>>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.SearchPending = stale.Task;
    var model = new RewardsProgramStatusesViewModel(service) { SearchQuery = "old" };
    var search = model.SearchAsync(TestContext.Current.CancellationToken);
    model.SearchQuery = "new";
    stale.SetResult([new("same", "First", "first", "rewards_program_status")]);
    await search;
    Assert.Empty(model.SearchRows);

    service.SearchResults = [
      new("same", "First", "first", "rewards_program_status"),
      new("same", "Duplicate", "duplicate", "rewards_program_status"),
      new("wrong", "Wrong", "wrong", "card")];
    await model.SearchAsync(TestContext.Current.CancellationToken);
    Assert.Single(model.SearchRows);
    Assert.Equal("same", model.SearchRows.Single().Value.Id);
  }
}
