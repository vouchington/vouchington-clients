using Voucha.Client.Core.Api;
using Voucha.Client.Core.Households;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Households;

public sealed class HouseholdViewModelTests
{
  [Fact]
  public async Task ReloadClearsStaleSectionsWhenOwnedAndMemberStreamsBothFail()
  {
    var service = new FakeHouseholdService();
    service.AccessHouseholdResults[HouseholdAccess.Owned] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdsResponse>>>([
      () => Task.FromResult(FakeHouseholdService.Page(FakeHouseholdService.Household("owned", "me"))),
      () => Task.FromException<Voucha.Client.Core.Api.HouseholdsResponse>(new HttpRequestException("owned failed")),
    ]);
    service.AccessHouseholdResults[HouseholdAccess.Member] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdsResponse>>>([
      () => Task.FromResult(FakeHouseholdService.Page(FakeHouseholdService.Household("shared", "other"))),
      () => Task.FromException<Voucha.Client.Core.Api.HouseholdsResponse>(new HttpRequestException("member failed")),
    ]);
    var viewModel = new HouseholdViewModel(service, "me");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["owned", "shared"], viewModel.Sections.Select(section => section.Id));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.Sections);
    Assert.True(viewModel.HasError);
    Assert.False(viewModel.CanCreate);
  }

  [Fact]
  public async Task LoadSelectsFirstOwnedAndEveryReadOnlyHouseholdInServerOrder()
  {
    var service = new FakeHouseholdService();
    service.HouseholdResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Page(
        FakeHouseholdService.Household("owned-first", "me"),
        FakeHouseholdService.Household("shared-one", "other"),
        FakeHouseholdService.Household("owned-second", "me"),
        FakeHouseholdService.Household("shared-two", "another"))));
    var viewModel = new HouseholdViewModel(service, "me");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("owned-first", viewModel.OwnedHousehold?.Id);
    Assert.Equal(["owned-first", "shared-one", "shared-two"], viewModel.Sections.Select(item => item.Id));
    Assert.Equal(["shared-one", "shared-two"], viewModel.MemberOnlyHouseholds.Select(item => item.Id));
    Assert.DoesNotContain(viewModel.Sections, item => item.Id == "owned-second");
    Assert.False(viewModel.CanCreate);
  }

  [Fact]
  public async Task MemberLoadsFailAndRetryIndependentlyWhilePreservingSuccessfulSections()
  {
    var service = new FakeHouseholdService();
    service.HouseholdResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Page(
        FakeHouseholdService.Household("owned", "me"), FakeHouseholdService.Household("shared", "other"))));
    service.MembershipResults["owned"] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdMembershipsResponse>>>([
      () => Task.FromResult(FakeHouseholdService.Members(FakeHouseholdService.Membership("member-a", "owned", "alice"))),
    ]);
    service.MembershipResults["shared"] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdMembershipsResponse>>>([
      () => Task.FromException<Voucha.Client.Core.Api.HouseholdMembershipsResponse>(new InvalidOperationException("shared failed")),
      () => Task.FromResult(FakeHouseholdService.Members(FakeHouseholdService.Membership("member-b", "shared", "bob"))),
    ]);
    var viewModel = new HouseholdViewModel(service, "me");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Single(viewModel.OwnedHousehold!.Members);
    Assert.True(viewModel.MemberOnlyHouseholds.Single().HasError);

    await viewModel.RetryMembershipsAsync("shared", TestContext.Current.CancellationToken);
    Assert.Single(viewModel.OwnedHousehold.Members);
    Assert.Equal("@bob", viewModel.MemberOnlyHouseholds.Single().Members.Single().LocalizedDisplayName);
  }

  [Fact]
  public async Task StaleListAndMembershipResponsesCannotOverwriteNewerState()
  {
    var service = new FakeHouseholdService();
    var oldList = new TaskCompletionSource<Voucha.Client.Core.Api.HouseholdsResponse>();
    var newList = new TaskCompletionSource<Voucha.Client.Core.Api.HouseholdsResponse>();
    service.HouseholdResults.Enqueue(() => oldList.Task);
    service.HouseholdResults.Enqueue(() => newList.Task);
    var viewModel = new HouseholdViewModel(service, "me");

    var first = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var second = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    newList.SetResult(FakeHouseholdService.Page(FakeHouseholdService.Household("new", "me")));
    await second;
    oldList.SetResult(FakeHouseholdService.Page(FakeHouseholdService.Household("old", "me")));
    await first;

    Assert.Equal("new", viewModel.OwnedHousehold?.Id);

    var oldMembers = new TaskCompletionSource<Voucha.Client.Core.Api.HouseholdMembershipsResponse>();
    var newMembers = new TaskCompletionSource<Voucha.Client.Core.Api.HouseholdMembershipsResponse>();
    service.MembershipResults["new"] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdMembershipsResponse>>>([
      () => oldMembers.Task,
      () => newMembers.Task,
    ]);
    var oldRetry = viewModel.RetryMembershipsAsync("new", TestContext.Current.CancellationToken);
    var newRetry = viewModel.RetryMembershipsAsync("new", TestContext.Current.CancellationToken);
    newMembers.SetResult(FakeHouseholdService.Members(FakeHouseholdService.Membership("new-member", "new", "new")));
    await newRetry;
    oldMembers.SetResult(FakeHouseholdService.Members(FakeHouseholdService.Membership("old-member", "new", "old")));
    await oldRetry;
    Assert.Equal("new-member", viewModel.OwnedHousehold!.Members.Single().Id);
  }

  [Fact]
  public async Task CreateIsAllowedWithoutOwnedHouseholdEvenWhenSharedHouseholdsExist()
  {
    var service = new FakeHouseholdService();
    service.HouseholdResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Page(
        FakeHouseholdService.Household("shared", "other"))));
    service.CreateResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Household("owned", "me")));
    var viewModel = new HouseholdViewModel(service, "me");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.CanCreate);
    await viewModel.CreateHouseholdAsync(TestContext.Current.CancellationToken);

    Assert.Equal("owned", viewModel.OwnedHousehold?.Id);
    Assert.Equal("shared", viewModel.MemberOnlyHouseholds.Single().Id);
    Assert.False(viewModel.CanCreate);
  }

  [Fact]
  public async Task CreationRequiresSuccessfulListAndRefreshFailureRevokesEligibility()
  {
    var service = new FakeHouseholdService();
    service.HouseholdResults.Enqueue(() =>
        Task.FromException<Voucha.Client.Core.Api.HouseholdsResponse>(new InvalidOperationException("initial failed")));
    service.HouseholdResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Page(
        FakeHouseholdService.Household("shared", "other"))));
    service.HouseholdResults.Enqueue(() =>
        Task.FromException<Voucha.Client.Core.Api.HouseholdsResponse>(new InvalidOperationException("refresh failed")));
    service.CreateResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Household("owned", "me")));
    var viewModel = new HouseholdViewModel(service, "me");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.CanCreate);
    await viewModel.CreateHouseholdAsync(TestContext.Current.CancellationToken);
    Assert.Single(service.CreateResults);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.CanCreate);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.CanCreate);
    await viewModel.CreateHouseholdAsync(TestContext.Current.CancellationToken);

    Assert.Single(service.CreateResults);
  }

  [Fact]
  public async Task RemoveIsOwnerOnlyOptimisticAndDeduplicatesSameMembership()
  {
    var service = ServiceWithOwnedMembers("a");
    var pending = new TaskCompletionSource();
    service.RemovalResults["a"] = new Queue<Func<Task>>([() => pending.Task]);
    var viewModel = new HouseholdViewModel(service, "me");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var section = viewModel.OwnedHousehold!;
    var member = section.Members.Single();

    var first = viewModel.RemoveMembershipAsync(section, member, TestContext.Current.CancellationToken);
    var duplicate = viewModel.RemoveMembershipAsync(section, member, TestContext.Current.CancellationToken);
    Assert.Empty(section.Members);
    Assert.Single(service.RemovalCalls);
    pending.SetResult();
    await Task.WhenAll(first, duplicate);

    var sharedService = new FakeHouseholdService();
    sharedService.HouseholdResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Page(
        FakeHouseholdService.Household("shared", "other"))));
    sharedService.MembershipResults["shared"] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdMembershipsResponse>>>([
      () => Task.FromResult(FakeHouseholdService.Members(FakeHouseholdService.Membership("shared-member", "shared"))),
    ]);
    var sharedViewModel = new HouseholdViewModel(sharedService, "me");
    await sharedViewModel.LoadAsync(TestContext.Current.CancellationToken);
    var sharedSection = sharedViewModel.MemberOnlyHouseholds.Single();
    await sharedViewModel.RemoveMembershipAsync(
        sharedSection,
        sharedSection.Members.Single(),
        TestContext.Current.CancellationToken);
    Assert.Single(sharedSection.Members);
    Assert.Empty(sharedService.RemovalCalls);
  }

  [Fact]
  public async Task ConcurrentDifferentRemovalFailuresRollbackAtOriginalPositionsWithoutDuplicates()
  {
    var service = ServiceWithOwnedMembers("a", "b", "c");
    var firstFailure = new TaskCompletionSource();
    var secondFailure = new TaskCompletionSource();
    service.RemovalResults["a"] = new Queue<Func<Task>>([() => firstFailure.Task]);
    service.RemovalResults["b"] = new Queue<Func<Task>>([() => secondFailure.Task]);
    var viewModel = new HouseholdViewModel(service, "me");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var section = viewModel.OwnedHousehold!;
    var a = section.Members[0];
    var b = section.Members[1];

    var removeA = viewModel.RemoveMembershipAsync(section, a, TestContext.Current.CancellationToken);
    var removeB = viewModel.RemoveMembershipAsync(section, b, TestContext.Current.CancellationToken);
    firstFailure.SetException(new InvalidOperationException("a failed"));
    await Assert.ThrowsAsync<InvalidOperationException>(async () => await firstFailure.Task);
    secondFailure.SetException(new InvalidOperationException("b failed"));
    await Task.WhenAll(removeA, removeB);

    Assert.Equal(["a", "b", "c"], section.Members.Select(item => item.Id));
    Assert.Equal(3, section.Members.Select(item => item.Id).Distinct().Count());
  }

  [Fact]
  public async Task RemovalFailureRestoresAfterAMembershipReloadDuringDeletion()
  {
    var service = ServiceWithOwnedMembers("a");
    var removal = new TaskCompletionSource();
    service.RemovalResults["a"] = new Queue<Func<Task>>([() => removal.Task]);
    var viewModel = new HouseholdViewModel(service, "me");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var section = viewModel.OwnedHousehold!;
    var remove = viewModel.RemoveMembershipAsync(
        section,
        section.Members.Single(),
        TestContext.Current.CancellationToken);

    service.MembershipResults["owned"] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdMembershipsResponse>>>([
      () => Task.FromResult(FakeHouseholdService.Members()),
    ]);
    await viewModel.RetryMembershipsAsync("owned", TestContext.Current.CancellationToken);
    removal.SetException(new InvalidOperationException("stale failure"));
    await remove;

    Assert.Equal(["a"], section.Members.Select(item => item.Id));
  }

  [Fact]
  public void MemberLabelsNeverExposeIdentifiersAndBlankRelationshipsAreHidden()
  {
    var membership = FakeHouseholdService.Membership("uuid-member", "uuid-household", null, " ");
    var row = HouseholdMemberRow.FromMembership(membership, canRemove: false);

    Assert.Equal("Household member", row.LocalizedDisplayName);
    Assert.Null(row.ProtocolRelationship);
    Assert.DoesNotContain("uuid", row.LocalizedDisplayName, StringComparison.OrdinalIgnoreCase);
  }

  private static FakeHouseholdService ServiceWithOwnedMembers(params string[] ids)
  {
    var service = new FakeHouseholdService();
    service.HouseholdResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Page(
        FakeHouseholdService.Household("owned", "me"))));
    service.MembershipResults["owned"] = new Queue<Func<Task<Voucha.Client.Core.Api.HouseholdMembershipsResponse>>>([
      () => Task.FromResult(FakeHouseholdService.Members(ids.Select(id =>
          FakeHouseholdService.Membership(id, "owned", id)).ToArray())),
    ]);
    return service;
  }
}
