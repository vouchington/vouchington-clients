using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.ModerationIntegrity;

public sealed class ModerationIntegrityApiServiceTests
{
  [Fact]
  public async Task AllFilterDecodesPendingAndResolvedRowsFromSharedDefaultFixtures()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse(
          "native.moderation.report-integrity.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse(
          "native.moderation.vote-integrity.default")),
    ]);
    var service = new ApiModerationIntegrityService(new VouchaApiClient(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var reports = await service.FetchReportFlagsAsync(
        IntegrityFlagStatus.All,
        cancellationToken: TestContext.Current.CancellationToken);
    var votes = await service.FetchVoteFlagsAsync(
        IntegrityFlagStatus.All,
        cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(2, reports.Results.Count);
    Assert.Contains(reports.Results, flag => flag.Resolution is null);
    Assert.Contains(reports.Results, flag => flag.Resolution == "dismissed");
    Assert.Equal(2, votes.Results.Count);
    Assert.Contains(votes.Results, flag => flag.Resolution is null);
    Assert.Contains(votes.Results, flag => flag.Resolution == "dismissed");
    Assert.Equal(
        "/api/v1/report-integrity/flags?limit=25",
        handler.Requests[0].PathAndQuery);
    Assert.Equal(
        "/api/v1/vote-integrity/flags?limit=25",
        handler.Requests[1].PathAndQuery);
  }

  [Fact]
  public async Task TypedMutationMethodsDecodeEverySharedResponseShape()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse(
          "native.moderation.report-integrity.resolution.dismissed")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse(
          "native.moderation.report-integrity.penalty")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse(
          "native.moderation.vote-integrity.resolution.suspended")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse(
          "native.moderation.vote-integrity.penalty")),
    ]);
    var service = new ApiModerationIntegrityService(new VouchaApiClient(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));
    var token = TestContext.Current.CancellationToken;

    var dismissed = await service.DismissReportFlagAsync("report-flag-1", token);
    var reportPenalty = await service.PenalizeReportersAsync("report-flag-1", token);
    var suspended = await service.ResolveVoteFlagAsync(
        "vote-flag-1", VoteIntegrityResolution.Suspended, token);
    var votePenalty = await service.ApplyVoteRingPenaltyAsync("vote-flag-1", token);

    Assert.Equal("dismissed", dismissed.Flag.Resolution);
    Assert.Equal("penalized", reportPenalty.Flag.Resolution);
    Assert.Equal(2, reportPenalty.Penalties.Count);
    Assert.Equal("suspended", suspended.Flag.Resolution);
    Assert.Equal(2, votePenalty.PenalizedUserCount);
    Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
    Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
    Assert.Equal(HttpMethod.Patch, handler.Requests[2].Method);
    Assert.Equal(HttpMethod.Post, handler.Requests[3].Method);
  }

  [Fact]
  public async Task VotePenaltyListRequiresTheRequestedFlagScope()
  {
    const string scoped = """
        {"results":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null},
         "filter_scope":{"source":"flag","source_flag_id":"flag-1"}}
        """;
    var handler = new RecordingHandler([
      new RecordedResponse(scoped),
      new RecordedResponse(scoped),
    ]);
    var service = new ApiModerationIntegrityService(new VouchaApiClient(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var response = await service.FetchVotePenaltiesAsync(
        IntegrityPenaltyStatus.All, sourceFlagId: "flag-1",
        cancellationToken: TestContext.Current.CancellationToken);
    await Assert.ThrowsAsync<InvalidOperationException>(() => service.FetchVotePenaltiesAsync(
        IntegrityPenaltyStatus.All, sourceFlagId: "flag-2",
        cancellationToken: TestContext.Current.CancellationToken));

    Assert.Empty(response.Results);
    Assert.Equal(
        "/api/v1/vote-integrity/penalties?limit=25&source=flag&source_flag_id=flag-1",
        handler.Requests[0].PathAndQuery);
  }
}
