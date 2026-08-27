using Voucha.Client.Core.Api;
using Voucha.Client.Core.Households;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Households;

public sealed class HouseholdInfrastructureTests
{
  [Fact]
  public void ApiServiceRejectsNullClient()
  {
    Assert.Throws<ArgumentNullException>(() => new ApiHouseholdService(null!));
  }

  [Fact]
  public async Task LegacyCancellationTokenOverloadsKeepUnpaginatedRoutes()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.households.empty")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.household-memberships.empty")),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    await client.FetchHouseholdsAsync(TestContext.Current.CancellationToken);
    await client.FetchHouseholdMembershipsAsync(
        "household one", TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/households", handler.Requests[0].PathAndQuery);
    Assert.Equal(
        "/api/v1/households/household%20one/memberships",
        handler.Requests[1].PathAndQuery);
  }

  [Fact]
  public async Task ApiServiceDelegatesEveryHouseholdOperationToItsTypedRoute()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.households.single-owned")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.households.create.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.household-memberships.single")),
      new RecordedResponse("{}"),
    ]);
    var service = new ApiHouseholdService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var households = await service.FetchHouseholdsPageAsync(
        HouseholdAccess.Owned,
        null,
        1,
        TestContext.Current.CancellationToken);
    var created = await service.CreateHouseholdAsync(TestContext.Current.CancellationToken);
    var memberships = await service.FetchMembershipsPageAsync(
        "household one",
        "members-after",
        25,
        TestContext.Current.CancellationToken);
    await service.RemoveMembershipAsync(
        "household one",
        "membership two",
        TestContext.Current.CancellationToken);

    Assert.Single(households.Results);
    Assert.Equal("00000000-0000-7000-8000-000000000101", created.Id);
    Assert.Single(memberships.Results);
    Assert.Equal(
        [
          new RecordedRequest(HttpMethod.Get, "/api/v1/households?access=owned&limit=1", null),
          new RecordedRequest(HttpMethod.Post, "/api/v1/households", "{}"),
          new RecordedRequest(
              HttpMethod.Get,
              "/api/v1/households/household%20one/memberships?after=members-after&limit=25",
              null),
          new RecordedRequest(
              HttpMethod.Delete,
              "/api/v1/households/household%20one/memberships/membership%20two",
              null),
        ],
        handler.Requests);
  }

  [Fact]
  public async Task CanceledListAndMembershipLoadsSetSectionAppropriateStates()
  {
    var service = new FakeHouseholdService();
    service.HouseholdResults.Enqueue(() => Task.FromCanceled<HouseholdsResponse>(new CancellationToken(true)));
    var viewModel = new HouseholdViewModel(service, "me");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(Voucha.Client.Core.Support.LoadState.Idle, viewModel.State);

    service.HouseholdResults.Enqueue(() => Task.FromResult(FakeHouseholdService.Page(
        FakeHouseholdService.Household("owned", "me"))));
    service.MembershipResults["owned"] = new Queue<Func<Task<HouseholdMembershipsResponse>>>([
      () => Task.FromCanceled<HouseholdMembershipsResponse>(new CancellationToken(true)),
    ]);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(Voucha.Client.Core.Support.LoadState.Loaded, viewModel.State);
    Assert.Equal(Voucha.Client.Core.Support.LoadState.Loaded, viewModel.OwnedHousehold!.State);
    Assert.Null(viewModel.OwnedHousehold.ErrorMessage);
    Assert.False(viewModel.OwnedHousehold.HasError);
  }
}
