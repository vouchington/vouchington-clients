using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Households;

public sealed class HouseholdApiTests
{
  [Fact]
  public void SharedFixturesDecodeListCreateAndNullableMembershipFields()
  {
    var empty = Decode<HouseholdsResponse>("native.households.empty");
    var single = Decode<HouseholdsResponse>("native.households.single-owned");
    var multiple = Decode<HouseholdsResponse>("native.households.multiple");
    var memberPage = Decode<HouseholdsResponse>("native.households.member.default");
    var memberPageTwo = Decode<HouseholdsResponse>("native.households.member.page-2");
    var memberships = Decode<HouseholdMembershipsResponse>("native.household-memberships.multiple");
    var membershipPageOne = Decode<HouseholdMembershipsResponse>("native.household-memberships.page-1");
    var membershipPageTwo = Decode<HouseholdMembershipsResponse>("native.household-memberships.page-2");
    var created = Decode<HouseholdCreateResponse>("native.households.create.default");

    Assert.Empty(empty.Results);
    Assert.Null(single.Results.Single().CreatedAt);
    Assert.Equal(2, multiple.Results.Count);
    Assert.True(memberPage.PageInfo.HasNextPage);
    Assert.False(memberPageTwo.PageInfo.HasNextPage);
    Assert.NotNull(membershipPageOne.PageInfo.EndCursor);
    Assert.False(membershipPageTwo.PageInfo.HasNextPage);
    Assert.Equal("household-alice", memberships.Results[0].Individual.Username);
    Assert.Null(memberships.Results[1].Individual.UserId);
    Assert.Null(memberships.Results[2].Relationship);
    Assert.NotNull(created.Household.CreatedAt);
  }

  [Fact]
  public void EndpointsMatchRoutesEscapeIdentifiersAndCreateWithEmptyBody()
  {
    var list = VouchaApiEndpoints.Households(HouseholdAccess.Member, "opaque+/=", 25);
    var create = VouchaApiEndpoints.CreateHousehold();
    var memberships = VouchaApiEndpoints.HouseholdMemberships("home/one", "members-cursor", 10);
    var delete = VouchaApiEndpoints.DeleteHouseholdMembership("home/one", "member/two");

    Assert.Equal(HttpMethod.Get, list.Method);
    Assert.Equal("/api/v1/households", list.Path);
    Assert.Equal("member", list.Query["access"]);
    Assert.Equal("opaque+/=", list.Query["after"]);
    Assert.Equal("25", list.Query["limit"]);
    Assert.Equal(HttpMethod.Post, create.Method);
    Assert.Equal("{}", JsonSerializer.Serialize(create.Body, VouchaApiJson.Options));
    Assert.Equal("/api/v1/households/home%2Fone/memberships", memberships.Path);
    Assert.Equal("members-cursor", memberships.Query["after"]);
    Assert.Equal("10", memberships.Query["limit"]);
    Assert.Equal("/api/v1/households/home%2Fone/memberships/member%2Ftwo", delete.Path);
    Assert.Equal(HttpMethod.Delete, delete.Method);
  }

  private static T Decode<T>(string fixtureId) =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options) ??
      throw new InvalidOperationException($"Fixture {fixtureId} decoded as null.");
}
