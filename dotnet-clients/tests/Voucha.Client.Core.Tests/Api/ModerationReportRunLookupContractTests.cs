using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationReportRunLookupContractTests
{
  [Fact]
  public void PendingLookupRetainsRequiredStatusAndNullableJudgementIdentity()
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.moderation.report-judgement-run.pending"))!.AsObject();
    var response = JsonSerializer.Deserialize<ModerationReportJudgementRunResponse>(body, VouchaApiJson.Options)!;
    Assert.Equal(ModerationRunStatus.Active, response.Status);
    Assert.False(response.IsTerminal);
    Assert.Null(response.JudgementId);
    var encoded = JsonSerializer.SerializeToNode(response, VouchaApiJson.Options)!.AsObject();
    Assert.Equal("active", encoded["status"]!.GetValue<string>());
    Assert.True(encoded.ContainsKey("judgement_id"));
    Assert.Null(encoded["judgement_id"]);
    foreach (var key in new[] { "status", "run_id", "judgement_id" })
    {
      var missing = body.DeepClone().AsObject();
      missing.Remove(key);
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<ModerationReportJudgementRunResponse>(missing, VouchaApiJson.Options));
    }
  }

  [Fact]
  public async Task LookupClientForwardsBothCanonicalIdentitiesAndReadsPendingOutcome()
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse("native.moderation.report-judgement-run.pending"));
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var client = new VouchaApiClient(http);

    var response = await client.GetModerationReportJudgementRunAsync(
        "019f6559-6b31-7171-b5b1-ccd9d702c45e", "00000000-0000-7000-8000-000000000303",
        TestContext.Current.CancellationToken);

    Assert.Equal(ModerationRunStatus.Active, response.Status);
    Assert.Null(response.JudgementId);
    var request = Assert.Single(handler.Requests);
    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal("/api/v1/reports/019f6559-6b31-7171-b5b1-ccd9d702c45e/judgements/00000000-0000-7000-8000-000000000303", request.PathAndQuery);
    Assert.Null(request.Body);
  }

  [Theory]
  [InlineData("succeeded", ModerationRunStatus.Succeeded, "00000000-0000-7000-8000-000000000304")]
  [InlineData("failed", ModerationRunStatus.Failed, null)]
  [InlineData("expired", ModerationRunStatus.Expired, null)]
  public void TerminalLookupsDecodeStatusAndJudgementIdentity(
      string fixture, ModerationRunStatus status, string? judgementId)
  {
    var response = JsonSerializer.Deserialize<ModerationReportJudgementRunResponse>(
        ApiFixtureLoader.LoadResponse($"native.moderation.report-judgement-run.{fixture}"),
        VouchaApiJson.Options)!;
    Assert.Equal(status, response.Status);
    Assert.True(response.IsTerminal);
    Assert.Equal(judgementId, response.JudgementId);
  }

  [Fact]
  public async Task ReportDetailAndDraftRunClientsReadSharedFixtures()
  {
    var detail = new RecordingHandler(ApiFixtureLoader.LoadResponse("native.moderation.report-detail.saved"));
    var detailResponse = await new VouchaApiClient(
        new HttpClient(detail) { BaseAddress = new Uri("https://api.test") })
        .GetModerationReportAsync("019f6559-6b31-7171-b5b1-ccd9d702c45e", TestContext.Current.CancellationToken);
    Assert.Equal("no_action", detailResponse.Judgement?.RecommendedAction);
    Assert.Equal("/api/v1/reports/019f6559-6b31-7171-b5b1-ccd9d702c45e", Assert.Single(detail.Requests).PathAndQuery);

    var run = new RecordingHandler(ApiFixtureLoader.LoadResponse("native.moderation.appeals.resolution-draft-run.succeeded"));
    var runResponse = await new VouchaApiClient(
        new HttpClient(run) { BaseAddress = new Uri("https://api.test") })
        .GetAppealResolutionDraftRunAsync("appeal-1", "run-1", TestContext.Current.CancellationToken);
    Assert.Equal(ModerationRunStatus.Succeeded, runResponse.Status);
    Assert.NotNull(runResponse.AiDraftedAt);
    Assert.Equal("/api/v1/appeals/appeal-1/resolution-drafts/run-1", Assert.Single(run.Requests).PathAndQuery);
  }
}
