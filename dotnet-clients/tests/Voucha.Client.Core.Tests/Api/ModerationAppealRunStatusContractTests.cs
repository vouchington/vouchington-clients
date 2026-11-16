using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationAppealRunStatusContractTests
{
  [Theory]
  [InlineData("native.moderation.appeals.detail.default")]
  [InlineData("native.moderation.appeals.update.default")]
  [InlineData("native.moderation.appeals.approval.default")]
  [InlineData("native.moderation.appeals.delivery.default")]
  [InlineData("native.moderation.appeals.resolution.accept")]
  [InlineData("native.moderation.appeals.resolution.reduce")]
  [InlineData("native.moderation.appeals.resolution.deny")]
  public void StaffAppealFixturesCarryTheRequestedRunStatus(string fixture)
  {
    var appeal = Deserialize(Staff(fixture));
    Assert.Equal(ModerationRunStatus.Succeeded, appeal.AiDraftRequestedRunStatus);
    var encoded = JsonSerializer.SerializeToNode(appeal, VouchaApiJson.Options)!.AsObject();
    Assert.Equal("succeeded", encoded["ai_draft_requested_run_status"]!.GetValue<string>());
  }

  [Theory]
  [InlineData("active", ModerationRunStatus.Active)]
  [InlineData("failed", ModerationRunStatus.Failed)]
  [InlineData("expired", ModerationRunStatus.Expired)]
  public void EveryRunStatusDecodesAndNullStaysNull(string wire, ModerationRunStatus expected)
  {
    var staff = Staff("native.moderation.appeals.detail.default");
    staff["ai_draft_requested_run_status"] = wire;
    Assert.Equal(expected, Deserialize(staff).AiDraftRequestedRunStatus);
    staff["ai_draft_requested_run_status"] = null;
    Assert.Null(Deserialize(staff).AiDraftRequestedRunStatus);
  }

  [Fact]
  public void UnknownRunStatusIsRejected()
  {
    var staff = Staff("native.moderation.appeals.detail.default");
    staff["ai_draft_requested_run_status"] = "queued";
    Assert.ThrowsAny<JsonException>(() => Deserialize(staff));
  }

  private static JsonObject Staff(string fixture) =>
      JsonNode.Parse(ApiFixtureLoader.LoadResponse(fixture))!["appeal"]!.AsObject();

  private static ModerationAppeal Deserialize(JsonObject value) =>
      JsonSerializer.Deserialize<ModerationAppeal>(value, VouchaApiJson.Options)!;
}
