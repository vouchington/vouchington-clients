using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationDisputeRunIdentityTests
{
  [Theory]
  [InlineData(null)]
  [InlineData("dispute-rerun-job-1")]
  public void StaffDisputeAcceptsNullAndPreservesRunIdentity(string? runId)
  {
    var dispute = DisputeObject();
    dispute["ai_draft_run_id"] = runId;
    var decoded = JsonSerializer.Deserialize<ModerationDispute>(dispute, VouchaApiJson.Options)!;
    Assert.Equal(runId, decoded.AiDraftRunId);
    var encoded = JsonSerializer.SerializeToNode(decoded, VouchaApiJson.Options)!;
    Assert.True(encoded.AsObject().ContainsKey("ai_draft_run_id"));
    Assert.Equal(runId, encoded["ai_draft_run_id"]?.GetValue<string>());
  }

  [Fact]
  public void MutationStaffProjectionRetainsRunIdentities()
  {
    var response = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.moderation.disputes.update.default"))!;
    var dispute = response["dispute"]!.AsObject();
    var decoded = JsonSerializer.Deserialize<ModerationDispute>(dispute, VouchaApiJson.Options)!;
    Assert.Equal(dispute["ai_draft_requested_run_id"]!.GetValue<string>(), decoded.AiDraftRequestedRunId);
    Assert.True(dispute.ContainsKey("ai_draft_run_id"));
    Assert.Equal(dispute["ai_draft_run_id"]?.GetValue<string>(), decoded.AiDraftRunId);
    var encoded = JsonSerializer.SerializeToNode(decoded, VouchaApiJson.Options)!.AsObject();
    Assert.True(encoded.ContainsKey("ai_draft_run_id"));
    Assert.Equal(dispute["ai_draft_run_id"]?.GetValue<string>(), encoded["ai_draft_run_id"]?.GetValue<string>());
  }

  [Fact]
  public void QueuedRerunPreservesRequiredRunIdentity()
  {
    var queued = JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.moderation.disputes.resolution-drafts.default"))!.AsObject();
    var runId = queued["run_id"]!.GetValue<string>();
    var decoded = JsonSerializer.Deserialize<ModerationDisputeQueueResponse>(queued, VouchaApiJson.Options)!;
    Assert.Equal(runId, decoded.RunId);
    Assert.Equal(runId, JsonSerializer.SerializeToNode(decoded, VouchaApiJson.Options)!["run_id"]?.GetValue<string>());
    queued.Remove("run_id");
    Assert.Throws<JsonException>(() =>
        JsonSerializer.Deserialize<ModerationDisputeQueueResponse>(queued, VouchaApiJson.Options));
  }

  private static JsonObject DisputeObject() =>
      JsonNode.Parse(ApiFixtureLoader.LoadResponse("native.moderation.disputes.detail.default"))!["dispute"]!.AsObject();
}
