using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class PublicAppealProjectionTests
{
  [Theory]
  [InlineData("native.moderation.appeals.detail.member.default")]
  [InlineData("native.moderation.appeals.detail.member.sent")]
  [InlineData("native.moderation.appeals.create.member.default")]
  public void CanonicalMemberProjectionNeverSynthesizesPrivateFields(string fixtureId)
  {
    var body = ApiFixtureLoader.LoadResponse(fixtureId);
    using var source = JsonDocument.Parse(body);
    var original = source.RootElement.GetProperty("appeal");
    var appeal = Assert.IsType<ModerationAppeal>(JsonSerializer.Deserialize<ModerationAppeal>(
        original, VouchaApiJson.Options));
    var encoded = JsonSerializer.SerializeToElement(appeal, VouchaApiJson.Options);
    Assert.Equal(original.GetProperty("id").GetString(), appeal.Id);
    foreach (var field in ProtectedFields)
    {
      Assert.False(encoded.TryGetProperty(field, out _), field);
    }
    foreach (var field in original.EnumerateObject())
    {
      if (field.Value.ValueKind != JsonValueKind.Null)
      {
        Assert.True(encoded.TryGetProperty(field.Name, out _), field.Name);
      }
    }
  }

  [Fact]
  public void EveryProtectedNullPreventsMemberDowngrade()
  {
    var source = JsonNode.Parse(ApiFixtureLoader.LoadResponse(
        "native.moderation.appeals.detail.member.default"))!;
    foreach (var field in ProtectedFields)
    {
      var appeal = source["appeal"]!.DeepClone().AsObject();
      appeal[field] = null;
      Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ModerationAppeal>(
          appeal, VouchaApiJson.Options));
    }
  }

  [Fact]
  public void SubmissionAndDeliveryKeepCanonicalMemberIdentity()
  {
    var submitted = Assert.IsType<ModerationAppealSubmissionResponse>(
        JsonSerializer.Deserialize<ModerationAppealSubmissionResponse>(ApiFixtureLoader.LoadResponse(
            "native.moderation.appeals.create.member.default"), VouchaApiJson.Options));
    Assert.False(submitted.IsDuplicate);
    Assert.Equal("00000000-0000-7000-8000-000000000103", submitted.Appeal.Id);
    var pending = Read("native.moderation.appeals.detail.member.default");
    var sent = Read("native.moderation.appeals.detail.member.sent");
    Assert.Equal(pending.Id, sent.Id);
    Assert.Null(pending.PublicResponse);
    Assert.Null(pending.SentAt);
    Assert.NotNull(sent.PublicResponse);
    Assert.NotNull(sent.SentAt);
  }

  private static ModerationAppeal Read(string fixtureId) =>
      Assert.IsType<ModerationAppealResponse>(JsonSerializer.Deserialize<ModerationAppealResponse>(
          ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options)).Appeal;

  private static readonly string[] ProtectedFields =
  [
    "case_id", "appellant_user_id", "appeal_reason", "recommended_action", "ai_public_response",
    "ai_internal_response", "model", "ai_drafted_at", "internal_notes", "drafted_at", "edited_at",
    "edited_by_id", "approved_by_id", "resolved_by_id", "latest_lifecycle_change_id", "staff_context",
    "ai_draft_requested_run_id", "ai_draft_run_id",
  ];
}
