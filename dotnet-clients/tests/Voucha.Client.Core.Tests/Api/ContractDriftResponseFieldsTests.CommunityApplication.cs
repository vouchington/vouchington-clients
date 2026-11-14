using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ContractDriftResponseFieldsTests
{
  [Fact]
  public void CommunityApplicationDetailPreservesThePendingApplication()
  {
    var response = Fixture<CommunityApplicationResponse>("web.communities.application.default");
    var application = response.CommunityApplication;
    Assert.Equal("application-1", application.Id);
    Assert.Equal("community-1", application.CommunityId);
    Assert.Equal("user-1", application.UserId);
    Assert.Equal("community_application", application.EntityType);
    Assert.Equal("I would like to join.", application.Message);
    Assert.Empty(application.Answers);
    Assert.Null(application.ReviewedAt);
    Assert.Null(application.ReviewedById);
    Assert.Null(application.ApprovedAt);
    Assert.Null(application.RejectedAt);
    Assert.Null(application.RejectionReason);
    Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), application.CreatedAt);
  }

  [Fact]
  public void CommunityApplicationDetailEscapesBothRouteSegments()
  {
    var request = VouchaApiEndpoints.CommunityApplication("community/name", "application/id");
    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal("/api/v1/communities/community%2Fname/applications/application%2Fid", request.Path);
  }
}
