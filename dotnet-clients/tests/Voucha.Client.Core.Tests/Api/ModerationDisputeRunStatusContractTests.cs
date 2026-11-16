using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationDisputeRunStatusContractTests
{
  [Theory]
  [InlineData("native.moderation.disputes.detail.default")]
  [InlineData("native.moderation.disputes.update.default")]
  [InlineData("native.moderation.disputes.approval.default")]
  [InlineData("native.moderation.disputes.delivery.default")]
  [InlineData("native.moderation.disputes.resolution.remove")]
  [InlineData("native.moderation.disputes.resolution.annotate")]
  [InlineData("native.moderation.disputes.resolution.dismiss")]
  public void StaffDisputeFixturesCarryTheRequestedRunIdentityAndStatus(string fixture)
  {
    var wire = Staff(fixture);
    var dispute = Deserialize(wire);

    Assert.Equal(wire["ai_draft_requested_run_id"]!.GetValue<string>(), dispute.AiDraftRequestedRunId);
    Assert.Equal(ModerationRunStatus.Active, dispute.AiDraftRequestedRunStatus);
    Assert.Equal(wire["ai_draft_run_id"]?.GetValue<string>(), dispute.AiDraftRunId);
    var encoded = JsonSerializer.SerializeToNode(dispute, VouchaApiJson.Options)!.AsObject();
    Assert.Equal("active", encoded["ai_draft_requested_run_status"]!.GetValue<string>());
  }

  [Theory]
  [InlineData("succeeded", ModerationRunStatus.Succeeded)]
  [InlineData("failed", ModerationRunStatus.Failed)]
  [InlineData("expired", ModerationRunStatus.Expired)]
  public void EveryRunStatusDecodesAndNullStaysNull(string wire, ModerationRunStatus expected)
  {
    var staff = Staff("native.moderation.disputes.detail.default");
    staff["ai_draft_requested_run_status"] = wire;
    Assert.Equal(expected, Deserialize(staff).AiDraftRequestedRunStatus);
    staff["ai_draft_requested_run_status"] = null;
    Assert.Null(Deserialize(staff).AiDraftRequestedRunStatus);
  }

  [Fact]
  public void UnknownRunStatusIsRejected()
  {
    var staff = Staff("native.moderation.disputes.detail.default");
    staff["ai_draft_requested_run_status"] = "queued";
    Assert.ThrowsAny<JsonException>(() => Deserialize(staff));
  }

  private static JsonObject Staff(string fixture) =>
      JsonNode.Parse(ApiFixtureLoader.LoadResponse(fixture))!["dispute"]!.AsObject();

  private static ModerationDispute Deserialize(JsonObject value) =>
      JsonSerializer.Deserialize<ModerationDispute>(value, VouchaApiJson.Options)!;
}
