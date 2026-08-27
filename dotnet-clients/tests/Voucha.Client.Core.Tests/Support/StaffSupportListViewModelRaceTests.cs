using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class StaffSupportListViewModelRaceTests
{
  [Fact]
  public async Task ThreadReplacementSearchCancelsAndRejectsTheStalePage()
  {
    var stale = new TaskCompletionSource<SupportThreadListResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var current = new TaskCompletionSource<SupportThreadListResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new StaffSupportTestService();
    var requests = 0;
    var staleToken = CancellationToken.None;
    var staleCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.ThreadFetch = (query, status, after, limit, cancellationToken) =>
    {
      Assert.Null(after);
      Assert.Equal(30, limit);
      if (requests++ == 0) { staleToken = cancellationToken; staleToken.Register(() => staleCancelled.TrySetResult()); Assert.Equal("old", query); Assert.Equal(StaffSupportThreadStatusFilter.Open, status); return stale.Task; }
      Assert.Equal("new", query); Assert.Equal(StaffSupportThreadStatusFilter.Resolved, status); return current.Task;
    };
    var viewModel = new StaffSupportThreadsViewModel(service) { Query = "old", Status = StaffSupportThreadStatusFilter.Open };

    var staleLoad = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.Query = "new";
    viewModel.Status = StaffSupportThreadStatusFilter.Resolved;
    var currentLoad = viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await staleCancelled.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.True(staleToken.IsCancellationRequested);
    current.SetResult(new([StaffSupportTestService.ThreadFor("new")], new("new", false, null)));
    await currentLoad;
    stale.SetResult(new([StaffSupportTestService.ThreadFor("old")], new("old", true, "old-cursor")));
    await staleLoad;

    Assert.Equal(["new"], viewModel.Threads.Select(item => item.Id));
    Assert.False(viewModel.HasMore);
    Assert.Equal([null, null], service.ThreadAfters);
  }

  [Fact]
  public async Task ContactReplacementSearchCancelsAndRejectsTheStalePage()
  {
    var stale = new TaskCompletionSource<SupportContactListResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var current = new TaskCompletionSource<SupportContactListResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new StaffSupportTestService();
    var requests = 0;
    var staleToken = CancellationToken.None;
    var staleCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.ContactFetch = (query, after, limit, cancellationToken) =>
    {
      Assert.Null(after);
      Assert.Equal(30, limit);
      if (requests++ == 0) { staleToken = cancellationToken; staleToken.Register(() => staleCancelled.TrySetResult()); Assert.Equal("old", query); return stale.Task; }
      Assert.Equal("new", query); return current.Task;
    };
    var viewModel = new StaffSupportContactsViewModel(service) { Query = "old" };

    var staleLoad = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.Query = "new";
    var currentLoad = viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await staleCancelled.Task.WaitAsync(TestContext.Current.CancellationToken);
    Assert.True(staleToken.IsCancellationRequested);
    current.SetResult(new([StaffSupportTestService.ContactFor("new")], new("new", false, null)));
    await currentLoad;
    stale.SetResult(new([StaffSupportTestService.ContactFor("old")], new("old", true, "old-cursor")));
    await staleLoad;

    Assert.Equal(["new"], viewModel.Contacts.Select(item => item.Id));
    Assert.False(viewModel.HasMore);
    Assert.Equal([null, null], service.ContactAfters);
  }
}
