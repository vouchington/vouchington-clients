using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationReportJudgementIdentityTests
{
  [Fact]
  public void StaffReportFixtureRetainsRequiredJudgementIdentity()
  {
    var page = JsonSerializer.Deserialize<StaffFlatModerationReportsResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.reports.default"), VouchaApiJson.Options)!;
    var judgement = Assert.IsType<ModerationReportJudgement>(Assert.Single(page.Reports).Judgement);
    var encoded = JsonSerializer.SerializeToNode(judgement, VouchaApiJson.Options)!;
    Assert.Equal("00000000-0000-7000-8000-000000000301", encoded["id"]?.GetValue<string>());
  }

  [Fact]
  public void NullJudgementIdentityIsRejected()
  {
    var judgement = JudgementObject();
    judgement["id"] = null;
    Assert.Throws<JsonException>(() =>
        JsonSerializer.Deserialize<ModerationReportJudgement>(judgement, VouchaApiJson.Options));
  }

  [Fact]
  public void MissingJudgementIdentityIsRejected()
  {
    var judgement = JudgementObject();
    judgement.Remove("id");
    Assert.Throws<JsonException>(() =>
        JsonSerializer.Deserialize<ModerationReportJudgement>(judgement, VouchaApiJson.Options));
  }

  [Fact]
  public void SameTextJudgementsRetainDistinctIdentities()
  {
    var original = JudgementObject();
    var rerun = (JsonObject)original.DeepClone();
    rerun["id"] = "00000000-0000-7000-8000-000000000302";
    var first = JsonSerializer.Deserialize<ModerationReportJudgement>(original, VouchaApiJson.Options)!;
    var second = JsonSerializer.Deserialize<ModerationReportJudgement>(rerun, VouchaApiJson.Options)!;
    Assert.Equal(first.PublicResponse, second.PublicResponse);
    var firstJson = JsonSerializer.SerializeToNode(first, VouchaApiJson.Options)!;
    var secondJson = JsonSerializer.SerializeToNode(second, VouchaApiJson.Options)!;
    Assert.Equal("00000000-0000-7000-8000-000000000301", firstJson["id"]?.GetValue<string>());
    Assert.Equal("00000000-0000-7000-8000-000000000302", secondJson["id"]?.GetValue<string>());
  }

  private static JsonObject JudgementObject()
  {
    var page = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.moderation.reports.default"))!;
    return page["results"]![0]!["judgement"]!.AsObject();
  }
}
