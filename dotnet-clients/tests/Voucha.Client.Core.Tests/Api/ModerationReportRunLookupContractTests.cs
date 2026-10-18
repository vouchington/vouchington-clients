using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationReportRunLookupContractTests
{
  [Fact]
  public void PendingLookupRetainsRequiredCompletionAndNullableJudgementIdentity()
  {
    var body = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.moderation.report-judgement-run.pending"))!.AsObject();
    var response = JsonSerializer.Deserialize<ModerationReportJudgementRunResponse>(body, VouchaApiJson.Options)!;
    Assert.False(response.Completed);
    Assert.Null(response.JudgementId);
    var encoded = JsonSerializer.SerializeToNode(response, VouchaApiJson.Options)!.AsObject();
    Assert.False(encoded["completed"]!.GetValue<bool>());
    Assert.True(encoded.ContainsKey("judgement_id"));
    Assert.Null(encoded["judgement_id"]);
    foreach (var key in new[] { "completed", "judgement_id" })
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

    Assert.False(response.Completed);
    Assert.Null(response.JudgementId);
    var request = Assert.Single(handler.Requests);
    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal("/api/v1/reports/019f6559-6b31-7171-b5b1-ccd9d702c45e/judgements/00000000-0000-7000-8000-000000000303", request.PathAndQuery);
    Assert.Null(request.Body);
  }
}
