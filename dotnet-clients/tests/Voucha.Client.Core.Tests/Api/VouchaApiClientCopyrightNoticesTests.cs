using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task CopyrightNoticeListPreservesTheCurrentPublicClaimant()
  {
    var (client, handler) = CreateClient("web.copyright.notices.default");
    var response = await client.FetchCopyrightNoticesAsync(cancellationToken: TestContext.Current.CancellationToken);
    AssertRequest(handler, HttpMethod.Get, "/api/v1/copyright-notices");
    Assert.Equal("Current claimant", Assert.Single(response.CopyrightNotices).Claimant?.DisplayName);
  }

  [Fact]
  public async Task PublicNoticeDetailKeepsNullClaimantExplicit()
  {
    var (client, handler) = CreateClient("web.copyright.notice.detail.null-claimant");
    var id = ApiFixtureLoader.RouteParameterValue("web.copyright.notice.detail.null-claimant", "id");
    var response = await client.FetchCopyrightNoticeAsync(id, TestContext.Current.CancellationToken);
    AssertRequest(handler, HttpMethod.Get, $"/api/v1/copyright-notices/{id}");
    Assert.Null(response.CopyrightNotice.Claimant);
  }

  [Fact]
  public async Task AuthorizedParticipantPreservesPublicClaimantAndStatements()
  {
    var (client, handler) = CreateClient("web.copyright.notice.participant.populated");
    var id = ApiFixtureLoader.RouteParameterValue("web.copyright.notice.participant.populated", "id");
    var response = await client.FetchCopyrightParticipantNoticeAsync(id, TestContext.Current.CancellationToken);
    AssertRequest(handler, HttpMethod.Get, $"/api/v1/copyright-notices/{id}/participant");
    Assert.Equal("Current claimant", response.CopyrightNotice.Claimant?.DisplayName);
    Assert.Single(response.CopyrightNotice.Statements);
  }

  [Fact]
  public async Task EuSettlementReadUsesTheCaseEndpointAndPreservesAnEmptyPage()
  {
    var (client, handler) = CreateClient("web.copyright.eu.dispute-settlements.participant");
    var id = ApiFixtureLoader.RouteParameterValue("web.copyright.eu.dispute-settlements.participant", "id");
    var response = await client.FetchCopyrightEuDisputeSettlementsAsync(id, cancellationToken: TestContext.Current.CancellationToken);
    AssertRequest(handler, HttpMethod.Get, $"/api/v1/copyright-notices/{id}/eu-dispute-settlements");
    Assert.Empty(response.CopyrightEuDisputeSettlements);
    Assert.False(response.PageInfo.HasNextPage);
    Assert.Null(response.PageInfo.EndCursor);
  }
}
