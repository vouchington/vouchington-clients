using Voucha.Client.Core.Api;
using Voucha.Client.Core.Households;
using Xunit;

namespace Voucha.Client.Core.Tests.Households;

public sealed class HouseholdPaginationTests
{
  [Fact]
  public async Task OwnedProbeAndMemberStreamUseIndependentTypedQueries()
  {
    var service = new FakeHouseholdService();
    service.AccessHouseholdResults[HouseholdAccess.Owned] = new([
      () => Task.FromResult(FakeHouseholdService.Page()),
    ]);
    service.AccessHouseholdResults[HouseholdAccess.Member] = new([
      () => Task.FromResult(FakeHouseholdService.PageWithMore(
          "household-cursor",
          FakeHouseholdService.Household("shared-a", "other"))),
      () => Task.FromResult(FakeHouseholdService.Page(
          FakeHouseholdService.Household("shared-a", "other"),
          FakeHouseholdService.Household("shared-b", "other"))),
    ]);
    var viewModel = new HouseholdViewModel(service, "me");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.CanCreate);
    Assert.Equal(["shared-a"], viewModel.MemberOnlyHouseholds.Select(item => item.Id));

    await viewModel.LoadMoreMemberHouseholdsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["shared-a", "shared-b"], viewModel.MemberOnlyHouseholds.Select(item => item.Id));
    Assert.Contains((HouseholdAccess.Owned, (string?)null, 1), service.HouseholdCalls);
    Assert.Contains((HouseholdAccess.Member, (string?)null, 25), service.HouseholdCalls);
    Assert.Contains((HouseholdAccess.Member, "household-cursor", 25), service.HouseholdCalls);
  }

  [Fact]
  public async Task MemberContinuationFailurePreservesRowsAndRetriesSameCursor()
  {
    var service = ServiceWithEmptyOwnedProbe();
    service.AccessHouseholdResults[HouseholdAccess.Member] = new([
      () => Task.FromResult(FakeHouseholdService.PageWithMore(
          "retry-cursor",
          FakeHouseholdService.Household("shared-a", "other"))),
      () => Task.FromException<HouseholdsResponse>(new HttpRequestException("offline")),
      () => Task.FromResult(FakeHouseholdService.Page(
          FakeHouseholdService.Household("shared-b", "other"))),
    ]);
    var viewModel = new HouseholdViewModel(service, "me");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreMemberHouseholdsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["shared-a"], viewModel.MemberOnlyHouseholds.Select(item => item.Id));
    Assert.True(viewModel.HasMemberHouseholdError);

    await viewModel.LoadMoreMemberHouseholdsAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["shared-a", "shared-b"], viewModel.MemberOnlyHouseholds.Select(item => item.Id));
    Assert.Equal(2, service.HouseholdCalls.Count(call => call.After == "retry-cursor"));
  }

  [Fact]
  public async Task MembershipPagesAppendDeduplicateAndRetryWithoutDroppingRows()
  {
    var service = new FakeHouseholdService();
    service.HouseholdResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Page(
        FakeHouseholdService.Household("owned", "me"))));
    service.MembershipResults["owned"] = new([
      () => Task.FromResult(FakeHouseholdService.MembersWithMore(
          "member-cursor",
          FakeHouseholdService.Membership("a", "owned"))),
      () => Task.FromException<HouseholdMembershipsResponse>(new HttpRequestException("offline")),
      () => Task.FromResult(FakeHouseholdService.Members(
          FakeHouseholdService.Membership("a", "owned"),
          FakeHouseholdService.Membership("b", "owned"))),
    ]);
    var viewModel = new HouseholdViewModel(service, "me");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var section = viewModel.OwnedHousehold!;

    await viewModel.LoadMembershipsAsync(section.Id, TestContext.Current.CancellationToken);
    Assert.Equal(["a"], section.Members.Select(item => item.Id));
    Assert.True(section.HasError);

    await viewModel.LoadMembershipsAsync(section.Id, TestContext.Current.CancellationToken);
    Assert.Equal(["a", "b"], section.Members.Select(item => item.Id));
  }

  [Fact]
  public async Task SuccessfulEmptyOwnedProbeAllowsCreateWhenMemberStreamFails()
  {
    var service = ServiceWithEmptyOwnedProbe();
    service.AccessHouseholdResults[HouseholdAccess.Member] = new([
      () => Task.FromException<HouseholdsResponse>(new HttpRequestException("members unavailable")),
    ]);
    service.CreateResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Household("owned", "me")));
    var viewModel = new HouseholdViewModel(service, "me");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.CanCreate);
    await viewModel.CreateHouseholdAsync(TestContext.Current.CancellationToken);

    Assert.Equal("owned", viewModel.OwnedHousehold?.Id);
  }

  [Fact]
  public async Task SuccessfulEmptyOwnedProbeEnablesCreateWhileMemberStreamIsStillLoading()
  {
    var owned = new TaskCompletionSource<HouseholdsResponse>();
    var members = new TaskCompletionSource<HouseholdsResponse>();
    var service = new FakeHouseholdService();
    service.AccessHouseholdResults[HouseholdAccess.Owned] = new([() => owned.Task]);
    service.AccessHouseholdResults[HouseholdAccess.Member] = new([() => members.Task]);
    var viewModel = new HouseholdViewModel(service, "me");
    var createEnabled = new TaskCompletionSource();
    viewModel.PropertyChanged += (_, eventArgs) =>
    {
      if (eventArgs.PropertyName == nameof(HouseholdViewModel.CanCreate) && viewModel.CanCreate)
        createEnabled.TrySetResult();
    };

    var load = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    owned.SetResult(FakeHouseholdService.Page());
    await createEnabled.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanCreate);
    Assert.False(load.IsCompleted);
    members.SetResult(FakeHouseholdService.Page());
    await load;
  }

  private static FakeHouseholdService ServiceWithEmptyOwnedProbe()
  {
    var service = new FakeHouseholdService();
    service.AccessHouseholdResults[HouseholdAccess.Owned] = new([
      () => Task.FromResult(FakeHouseholdService.Page()),
    ]);
    return service;
  }
}
