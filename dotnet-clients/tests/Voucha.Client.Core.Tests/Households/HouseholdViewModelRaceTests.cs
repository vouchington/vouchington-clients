using Voucha.Client.Core.Households;
using Xunit;

namespace Voucha.Client.Core.Tests.Households;

public sealed class HouseholdViewModelRaceTests
{
  [Fact]
  public async Task MemberResponsePausedBeforeApplyCannotOverwriteReload()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var staleMemberResponse = new TaskCompletionSource<Voucha.Client.Core.Api.HouseholdsResponse>();
    var service = new FakeHouseholdService();
    service.AccessHouseholdResults[Voucha.Client.Core.Api.HouseholdAccess.Owned] = new([
      () => Task.FromResult(FakeHouseholdService.Page()),
      () => Task.FromResult(FakeHouseholdService.Page()),
    ]);
    service.AccessHouseholdResults[Voucha.Client.Core.Api.HouseholdAccess.Member] = new([
      () => staleMemberResponse.Task,
      () => Task.FromResult(FakeHouseholdService.Page(
          FakeHouseholdService.Household("fresh", "other"))),
    ]);
    var viewModel = new HouseholdViewModel(service, "me");
    var staleApplyReachedGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var releaseStaleApply = new ManualResetEventSlim();
    var shouldBlock = 1;
    viewModel.BeforeMemberHouseholdApply = () =>
    {
      if (Interlocked.Exchange(ref shouldBlock, 0) == 0) return;
      staleApplyReachedGate.SetResult();
      releaseStaleApply.Wait();
    };

    var staleLoad = Task.Run(() => viewModel.LoadAsync(cancellationToken), cancellationToken);
    var staleSetter = Task.Run(() => staleMemberResponse.SetResult(FakeHouseholdService.Page(
        FakeHouseholdService.Household("stale", "other"))), cancellationToken);
    await staleApplyReachedGate.Task.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
    try
    {
      await viewModel.LoadAsync(cancellationToken);
      Assert.Equal(["fresh"], viewModel.Sections.Select(section => section.Id));
    }
    finally
    {
      releaseStaleApply.Set();
    }

    await Task.WhenAll(staleLoad, staleSetter);
    Assert.Equal(["fresh"], viewModel.Sections.Select(section => section.Id));
  }

  [Fact]
  public async Task MembershipLoadStartedBeforeSuccessfulRemovalCannotRestoreMember()
  {
    var context = await CreateLoadedContextAsync();
    var staleLoad = new TaskCompletionSource<Voucha.Client.Core.Api.HouseholdMembershipsResponse>();
    context.Service.MembershipResults["owned"] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdMembershipsResponse>>>([
      () => staleLoad.Task,
    ]);

    var load = context.ViewModel.RetryMembershipsAsync("owned", TestContext.Current.CancellationToken);
    await context.ViewModel.RemoveMembershipAsync(
        context.Section,
        context.Section.Members.Single(),
        TestContext.Current.CancellationToken);
    staleLoad.SetResult(FakeHouseholdService.Members(FakeHouseholdService.Membership("a", "owned")));
    await load;

    Assert.Empty(context.Section.Members);
    Assert.False(context.Section.IsLoading);
  }

  [Fact]
  public async Task MembershipLoadStartedBeforeFailedRemovalCannotReplaceRollback()
  {
    var context = await CreateLoadedContextAsync();
    var staleLoad = new TaskCompletionSource<Voucha.Client.Core.Api.HouseholdMembershipsResponse>();
    context.Service.MembershipResults["owned"] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdMembershipsResponse>>>([
      () => staleLoad.Task,
    ]);
    context.Service.RemovalResults["a"] = new Queue<Func<Task>>([
      () => Task.FromException(new InvalidOperationException("failed")),
    ]);

    var load = context.ViewModel.RetryMembershipsAsync("owned", TestContext.Current.CancellationToken);
    await context.ViewModel.RemoveMembershipAsync(
        context.Section,
        context.Section.Members.Single(),
        TestContext.Current.CancellationToken);
    staleLoad.SetResult(FakeHouseholdService.Members(FakeHouseholdService.Membership("stale", "owned")));
    await load;

    Assert.Equal(["a"], context.Section.Members.Select(item => item.Id));
    Assert.False(context.Section.IsLoading);
  }

  [Fact]
  public async Task SuccessfulCreateSurvivesAListThatStartedWhileCreateWasPending()
  {
    var service = new FakeHouseholdService();
    service.HouseholdResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Page()));
    var createResult = new TaskCompletionSource<Voucha.Client.Core.Api.Household>();
    var staleList = new TaskCompletionSource<Voucha.Client.Core.Api.HouseholdsResponse>();
    service.CreateResults.Enqueue(() => createResult.Task);
    service.HouseholdResults.Enqueue(() => staleList.Task);
    var viewModel = new HouseholdViewModel(service, "me");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var create = viewModel.CreateHouseholdAsync(TestContext.Current.CancellationToken);
    var load = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.CanCreate);
    createResult.SetResult(FakeHouseholdService.Household("created", "me"));
    await create;
    staleList.SetResult(FakeHouseholdService.Page());
    await load;

    Assert.Equal("created", viewModel.OwnedHousehold?.Id);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task InitialLoadingDisablesCreate()
  {
    var service = new FakeHouseholdService();
    var list = new TaskCompletionSource<Voucha.Client.Core.Api.HouseholdsResponse>();
    service.HouseholdResults.Enqueue(() => list.Task);
    var viewModel = new HouseholdViewModel(service, "me");

    var load = viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsLoading);
    Assert.False(viewModel.CanCreate);
    list.SetResult(FakeHouseholdService.Page());
    await load;
  }

  [Fact]
  public async Task MembershipLoadStartedDuringSuccessfulRemovalCannotRestoreMember()
  {
    var context = await CreateLoadedContextAsync();
    var removal = new TaskCompletionSource();
    var loadResult = new TaskCompletionSource<Voucha.Client.Core.Api.HouseholdMembershipsResponse>();
    context.Service.RemovalResults["a"] = new Queue<Func<Task>>([() => removal.Task]);
    context.Service.MembershipResults["owned"] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdMembershipsResponse>>>([
      () => loadResult.Task,
    ]);

    var remove = context.ViewModel.RemoveMembershipAsync(
        context.Section, context.Section.Members.Single(), TestContext.Current.CancellationToken);
    var load = context.ViewModel.RetryMembershipsAsync("owned", TestContext.Current.CancellationToken);
    removal.SetResult();
    await remove;
    loadResult.SetResult(FakeHouseholdService.Members(FakeHouseholdService.Membership("a", "owned")));
    await load;

    Assert.Empty(context.Section.Members);
    Assert.False(context.Section.IsLoading);
  }

  [Fact]
  public async Task MembershipLoadDuringFailedRemovalCannotOverrideSingleRollback()
  {
    var context = await CreateLoadedContextAsync();
    var removal = new TaskCompletionSource();
    var loadResult = new TaskCompletionSource<Voucha.Client.Core.Api.HouseholdMembershipsResponse>();
    context.Service.RemovalResults["a"] = new Queue<Func<Task>>([() => removal.Task]);
    context.Service.MembershipResults["owned"] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdMembershipsResponse>>>([
      () => loadResult.Task,
    ]);

    var remove = context.ViewModel.RemoveMembershipAsync(
        context.Section, context.Section.Members.Single(), TestContext.Current.CancellationToken);
    var load = context.ViewModel.RetryMembershipsAsync("owned", TestContext.Current.CancellationToken);
    loadResult.SetResult(FakeHouseholdService.Members(FakeHouseholdService.Membership("a", "owned")));
    await load;
    removal.SetException(new InvalidOperationException("failed"));
    await remove;

    Assert.Equal(["a"], context.Section.Members.Select(item => item.Id));
  }

  private static async Task<(FakeHouseholdService Service, HouseholdViewModel ViewModel, HouseholdSection Section)>
      CreateLoadedContextAsync()
  {
    var service = new FakeHouseholdService();
    service.HouseholdResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Page(
        FakeHouseholdService.Household("owned", "me"))));
    service.MembershipResults["owned"] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdMembershipsResponse>>>([
      () => Task.FromResult(FakeHouseholdService.Members(FakeHouseholdService.Membership("a", "owned"))),
    ]);
    var viewModel = new HouseholdViewModel(service, "me");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    return (service, viewModel, viewModel.OwnedHousehold!);
  }
}
