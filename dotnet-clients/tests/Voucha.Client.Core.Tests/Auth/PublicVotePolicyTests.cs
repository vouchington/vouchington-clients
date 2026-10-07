using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class PublicVotePolicyTests
{
  [Theory]
  [InlineData(null, false)]
  [InlineData("user", true)]
  [InlineData("administrator", true)]
  [InlineData("investor", true)]
  public void PublicVotePolicyUsesAccountTypeInsteadOfRoles(string? role, bool expected)
  {
    var session = role is null
        ? SessionSnapshot.Anonymous
        : new SessionSnapshot(new User(
            "user-1",
            "alice",
            Roles: [role],
            EmailAddress: "a@example.com",
            MembershipPlan: "free"));

    Assert.Equal(expected, session.CanCastPublicVotes());
  }

  [Theory]
  [InlineData(AccountType.Official)]
  [InlineData(AccountType.System)]
  [InlineData(AccountType.AiAgent)]
  public void PublicVotePolicyExcludesPlatformAccounts(AccountType accountType)
  {
    var session = new SessionSnapshot(new User(
        "agent-1",
        "agent",
        Roles: ["user"],
        AccountType: accountType));

    Assert.False(session.CanCastPublicVotes());
  }

  [Fact]
  public void OfficialAccountCanClearAnExistingBallotButCannotCastOrChangeOne()
  {
    var official = new SessionSnapshot(new User(
        "agent-1",
        "agent",
        Roles: ["user"],
        AccountType: AccountType.Official));

    Assert.False(official.CanCastPublicVotes());
    Assert.False(official.CanClearPublicVote(null));
    Assert.True(official.CanClearPublicVote(ElectionVoteChoice.Vouch));
  }

  [Fact]
  public void PermittedViewerRetractsWithNeutralInsteadOfClear()
  {
    var viewer = new SessionSnapshot(new User("user-1", "alice", Roles: ["user"]));

    Assert.True(viewer.CanCastPublicVotes());
    Assert.False(viewer.CanClearPublicVote(null));
    Assert.False(viewer.CanClearPublicVote(ElectionVoteChoice.Vouch));
  }

  [Fact]
  public void AnonymousViewerCannotClearAStoredBallot()
  {
    Assert.False(SessionSnapshot.Anonymous.CanClearPublicVote(ElectionVoteChoice.Vouch));
  }

  [Theory]
  [InlineData("user", false, true)]
  [InlineData("administrator", false, true)]
  [InlineData("investor", false, true)]
  [InlineData("investor", true, false)]
  [InlineData("administrator", true, true)]
  public void RelationVotePolicyAllowsOfficialStructuralRelationsButRestrictsUserTags(
      string role,
      bool isUserTag,
      bool expected)
  {
    var session = new SessionSnapshot(new User(
        "user-1",
        "alice",
        Roles: [role],
        AccountType: role == "user" ? null : AccountType.Official));

    Assert.Equal(expected, session.CanCreateEntityRelationVote(isUserTag));
  }

  [Fact]
  public void AnonymousViewerCannotCreateAnyEntityRelationVote()
  {
    Assert.False(SessionSnapshot.Anonymous.CanCreateEntityRelationVote(isUserTag: false));
    Assert.False(SessionSnapshot.Anonymous.CanCreateEntityRelationVote(isUserTag: true));
  }

  [Theory]
  [InlineData(null, null, false, false)]
  [InlineData("free", "user", false, false)]
  [InlineData("plus", "user", true, true)]
  [InlineData("PLUS", "user", true, true)]
  [InlineData("pro", "user", true, true)]
  [InlineData("free", "administrator", true, false)]
  public void PaidFeaturePoliciesKeepCrawlAdministrationSeparateFromOwnerAnalytics(
      string? membershipPlan,
      string? role,
      bool expectedCrawlHistory,
      bool expectedLandingAnalytics)
  {
    var session = membershipPlan is null
        ? SessionSnapshot.Anonymous
        : new SessionSnapshot(new User("user-1", "alice", Roles: [role!], MembershipPlan: membershipPlan));

    Assert.Equal(expectedCrawlHistory, session.CanViewPaidCrawlHistory());
    Assert.Equal(expectedLandingAnalytics, session.CanViewLandingPageAnalytics());
  }

  [Fact]
  public void LandingAnalyticsPolicyDeniesExpiredMembership()
  {
    var membership = new Membership(
        "membership", "membership-1", "user-1", "plus", "active", DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow.AddSeconds(-1),
        null, null, true, null, null, null, null, null, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
        new MembershipSku("sku-1", "plus", new Money(500, "usd"), "monthly", "price-1"));

    Assert.False(membership.CanViewLandingPageAnalytics());
  }
}
