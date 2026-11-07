using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task IntegrityReviewClientsForwardOpaquePaginationAndDecodeReviewResults()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.vote-integrity.penalties.review-due")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.vote-integrity.penalties.review")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.report-integrity.penalties.review-due")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.report-integrity.penalties.review")),
    ]);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var client = new VouchaApiClient(http);
    var token = TestContext.Current.CancellationToken;

    var voteDue = await client.FetchVoteIntegrityPenaltyReviewsDueAsync("opaque+/=", 7, token);
    var voteReview = await client.ReviewVoteIntegrityPenaltyAsync("vote/id", token);
    var reportDue = await client.FetchReportIntegrityPenaltyReviewsDueAsync("next cursor", 9, token);
    var reportReview = await client.ReviewReportIntegrityPenaltyAsync("report/id", token);

    Assert.Single(voteDue.Results);
    Assert.NotNull(voteReview.Penalty.ReviewedAt);
    Assert.Single(reportDue.Results);
    Assert.NotNull(reportReview.Penalty.ReviewedAt);
    Assert.Equal("/api/v1/vote-integrity/penalties/review-due?after=opaque%2B%2F%3D&limit=7", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/vote-integrity/penalties/vote%2Fid/reviews", handler.Requests[1].PathAndQuery);
    Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
    Assert.Equal("/api/v1/report-integrity/penalties/review-due?after=next%20cursor&limit=9", handler.Requests[2].PathAndQuery);
    Assert.Equal("/api/v1/report-integrity/penalties/report%2Fid/reviews", handler.Requests[3].PathAndQuery);
    Assert.Equal(HttpMethod.Post, handler.Requests[3].Method);
  }

  [Fact]
  public async Task CopyrightStaffClientsForwardFiltersAndDecodeNamedResponseFields()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse("web.copyright.staff-queue.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("web.copyright.email-notice-association-search")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.copyright.repeat-infringer-accounts")),
    ]);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var client = new VouchaApiClient(http);
    var token = TestContext.Current.CancellationToken;

    var queue = await client.FetchCopyrightStaffQueueAsync("opaque+/=", 5, token);
    var associations = await client.SearchCopyrightAssociationTargetsAsync("Harbour photo", "next cursor", 6, token);
    var accounts = await client.FetchCopyrightRepeatInfringerAccountsAsync("notice/id", token);

    Assert.Single(queue.CopyrightNotices);
    Assert.Equal("Harbour photograph", Assert.Single(associations.CopyrightNotices).WorkDescription);
    Assert.Equal("photo-author", Assert.Single(accounts.Accounts).AccountUsername);
    Assert.Equal("/api/v1/copyright-notices/review-queue?after=opaque%2B%2F%3D&limit=5", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/copyright-notices/association-targets?after=next%20cursor&limit=6&q=Harbour%20photo", handler.Requests[1].PathAndQuery);
    Assert.Equal("/api/v1/copyright-notices/notice%2Fid/repeat-infringer-accounts", handler.Requests[2].PathAndQuery);
    Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
  }
}
