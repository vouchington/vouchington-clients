using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationCurrentRunContractTests
{
  [Fact]
  public void StaffDisputeRequiresRequestedAndSavedRunIdentities() =>
      AssertStaffIdentities<ModerationDispute>(FixtureObject("native.moderation.disputes.detail.default", "dispute"));

  [Fact]
  public void StaffAppealRequiresRequestedAndSavedRunIdentities() =>
      AssertStaffIdentities<ModerationAppeal>(FixtureObject("native.moderation.appeals.detail.default", "appeal"));

  [Fact]
  public void ReportAdmissionRetainsRequiredRunIdentity()
  {
    var admission = FixtureObject("native.moderation.report-judgement.default");
    var encoded = RoundTrip<ModerationReportJudgementResponse>(admission);
    Assert.Equal(admission["run_id"]!.GetValue<string>(), encoded["run_id"]?.GetValue<string>());
    admission.Remove("run_id");
    Assert.Throws<JsonException>(() => Deserialize<ModerationReportJudgementResponse>(admission));
  }

  [Fact]
  public void StaffReportRequiresAutomaticJudgementEligibility()
  {
    var page = FixtureObject("native.moderation.reports.default");
    var report = page["results"]!.AsArray()[0]!.AsObject();
    AssertEligibility<StaffModerationReport>(report);
    var community = FixtureObject("native.community.pending-reports.paginated");
    AssertEligibility<CommunityPendingReport>(community["reports"]!.AsArray()[0]!.AsObject());
  }

  [Theory]
  [InlineData("native.moderation.report-judgement.default", typeof(ModerationReportJudgementResponse))]
  [InlineData("native.moderation.appeals.resolution-drafts.default", typeof(ModerationAppealQueueResponse))]
  [InlineData("native.moderation.disputes.resolution-drafts.default", typeof(ModerationDisputeQueueResponse))]
  public void AdmissionsRejectNullRunIdentities(string fixture, Type responseType)
  {
    var body = FixtureObject(fixture);
    body["run_id"] = null;
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(body, responseType, VouchaApiJson.Options));
  }

  [Theory]
  [InlineData("native.moderation.disputes.detail.default", "dispute", typeof(ModerationDispute))]
  [InlineData("native.moderation.appeals.detail.default", "appeal", typeof(ModerationAppeal))]
  public void StaffProtectedFieldsRejectDuplicateAndNoncanonicalNames(string fixture, string key, Type responseType)
  {
    var body = FixtureObject(fixture, key).ToJsonString();
    foreach (var spelling in new[]
    {
      "ai_draft_requested_run_id", "AI_DRAFT_REQUESTED_RUN_ID", "model", "AI_DRAFT_RUN_ID",
    })
    {
      var malformed = body.Insert(1, $"\"{spelling}\":null,");
      Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(malformed, responseType, VouchaApiJson.Options));
    }
  }

  private static void AssertStaffIdentities<T>(JsonObject staff)
  {
    var encoded = RoundTrip<T>(staff);
    foreach (var field in new[] { "ai_draft_requested_run_id", "ai_draft_run_id" })
    {
      Assert.True(staff.ContainsKey(field), field);
      Assert.Equal(staff[field]?.GetValue<string>(), encoded[field]?.GetValue<string>());
      var missing = staff.DeepClone().AsObject();
      missing.Remove(field);
      Assert.Throws<JsonException>(() => Deserialize<T>(missing));
    }
    var invalidRequested = staff.DeepClone().AsObject();
    invalidRequested["ai_draft_requested_run_id"] = null;
    Assert.Throws<JsonException>(() => Deserialize<T>(invalidRequested));
    var nullableSaved = staff.DeepClone().AsObject();
    nullableSaved["ai_draft_run_id"] = null;
    var nullableEncoded = RoundTrip<T>(nullableSaved);
    Assert.True(nullableEncoded.ContainsKey("ai_draft_run_id"));
    Assert.Null(nullableEncoded["ai_draft_run_id"]);
  }

  private static void AssertEligibility<T>(JsonObject report)
  {
    var encoded = RoundTrip<T>(report);
    Assert.Equal(report["automatic_judgement_enabled"]!.GetValue<bool>(),
        encoded["automatic_judgement_enabled"]?.GetValue<bool>());
    var missing = report.DeepClone().AsObject();
    missing.Remove("automatic_judgement_enabled");
    Assert.Throws<JsonException>(() => Deserialize<T>(missing));
  }

  private static JsonObject FixtureObject(string id, string? key = null)
  {
    var value = JsonNode.Parse(ApiFixtureLoader.LoadResponse(id))!.AsObject();
    return key is null ? value : value[key]!.AsObject();
  }

  private static T Deserialize<T>(JsonObject value) =>
      JsonSerializer.Deserialize<T>(value, VouchaApiJson.Options)!;

  private static JsonObject RoundTrip<T>(JsonObject value) =>
      JsonSerializer.SerializeToNode(Deserialize<T>(value), VouchaApiJson.Options)!.AsObject();
}
