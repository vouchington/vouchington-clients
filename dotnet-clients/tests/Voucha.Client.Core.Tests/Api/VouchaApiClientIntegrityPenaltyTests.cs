using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiClientIntegrityPenaltyTests
{
  [Fact]
  public async Task PenaltyMethodsUseScopedListAndExactMutationRoutes()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.report-integrity.penalties.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.report-integrity.penalties.get")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.report-integrity.penalties.revoke")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.vote-integrity.penalties.default")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.vote-integrity.penalties.get")),
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.vote-integrity.penalties.revoke")),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new("https://api.test") });
    var token = TestContext.Current.CancellationToken;

    await client.FetchReportIntegrityPenaltiesAsync(
        IntegrityPenaltyStatus.All, "cursor-r", 12, "user-r", "flag-r", token);
    await client.FetchReportIntegrityPenaltyAsync("penalty-r", token);
    var report = await client.RevokeReportIntegrityPenaltyAsync("penalty-r", token);
    await client.FetchVoteIntegrityPenaltiesAsync(
        IntegrityPenaltyStatus.Revoked, "cursor-v", 13, "user-v", "flag-v", token);
    await client.FetchVoteIntegrityPenaltyAsync("penalty-v", token);
    var vote = await client.RevokeVoteIntegrityPenaltyAsync("penalty-v", token);

    Assert.Equal("/api/v1/report-integrity/penalties?after=cursor-r&limit=12&source_flag_id=flag-r&user_id=user-r", handler.Requests[0].PathAndQuery);
    Assert.Equal("/api/v1/report-integrity/penalties/penalty-r", handler.Requests[1].PathAndQuery);
    Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
    Assert.Equal("/api/v1/vote-integrity/penalties?after=cursor-v&limit=13&source=flag&source_flag_id=flag-v&status=revoked&user_id=user-v", handler.Requests[3].PathAndQuery);
    Assert.Equal("/api/v1/vote-integrity/penalties/penalty-v", handler.Requests[4].PathAndQuery);
    Assert.Equal(HttpMethod.Delete, handler.Requests[5].Method);
    Assert.NotNull(report.Penalty.RevokedAt);
    Assert.NotNull(vote.Penalty.RevokedAt);
  }
}
