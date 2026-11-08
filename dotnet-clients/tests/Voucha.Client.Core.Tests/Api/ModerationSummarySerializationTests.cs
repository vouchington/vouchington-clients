using System.Text.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationSummarySerializationTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void AppealListSerializationPreservesStaffSummaryWithoutPublishingPrivateDetail(bool includeNulls)
  {
    var options = Options(includeNulls);
    var page = JsonSerializer.Deserialize<ModerationAppealListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.appeals.default"), options)!;
    var summary = Assert.Single(page.Appeals);
    var detail = JsonSerializer.Deserialize<ModerationAppealResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.appeals.detail.default"), options)!.Appeal;
    Assert.NotNull(detail.AiDraftRequestedRunId);
    Assert.NotNull(detail.AiPublicResponse);
    var enriched = summary with
    {
      AiPublicResponse = detail.AiPublicResponse,
      AiInternalResponse = detail.AiInternalResponse,
      AiDraftRequestedRunId = detail.AiDraftRequestedRunId,
      AiDraftRunId = detail.AiDraftRunId,
      StaffContext = detail.StaffContext,
      InternalNotes = detail.InternalNotes,
      AppealReason = detail.AppealReason,
    };

    var encoded = JsonSerializer.SerializeToElement(new ModerationAppealListResponse([enriched], page.PageInfo), options);
    var row = encoded.GetProperty("appeals")[0];
    Assert.Equal("appealing-reviewer", row.GetProperty("appellant").GetProperty("username").GetString());
    Assert.True(row.GetProperty("has_ai_draft").GetBoolean());
    Assert.False(row.GetProperty("has_draft").GetBoolean());
    Assert.Equal("A removed review", row.GetProperty("target_context").GetProperty("title").GetString());
    AssertPrivateDetailAbsent(row);
    Assert.False(row.TryGetProperty("appeal_reason", out _));
    var decoded = encoded.Deserialize<ModerationAppealListResponse>(options)!;
    Assert.Equal(page.PageInfo.EndCursor, decoded.PageInfo.EndCursor);
    Assert.True(decoded.PageInfo.HasMorePages);
    Assert.Null(Assert.Single(decoded.Appeals).AiDraftRequestedRunId);
    Assert.Equal(summary.Appellant, decoded.Appeals[0].Appellant);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void DisputeListSerializationPreservesReviewSummaryWithoutPublishingPrivateDetail(bool includeNulls)
  {
    var options = Options(includeNulls);
    var page = JsonSerializer.Deserialize<ModerationDisputeListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.default"), options)!;
    var summary = Assert.Single(page.Disputes);
    var detail = JsonSerializer.Deserialize<ModerationDisputeResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.detail.default"), options)!.Dispute;
    Assert.NotNull(detail.ClaimText);
    var enriched = summary with
    {
      ClaimText = detail.ClaimText,
      AiPublicResponse = detail.AiPublicResponse,
      AiDraftRequestedRunId = detail.AiDraftRequestedRunId,
      StaffContext = detail.StaffContext,
      InternalNotes = detail.InternalNotes,
    };

    var encoded = JsonSerializer.SerializeToElement(new ModerationDisputeListResponse([enriched], page.PageInfo), options);
    var row = encoded.GetProperty("disputes")[0];
    Assert.Equal("native-claimant", row.GetProperty("disputant").GetProperty("username").GetString());
    Assert.Equal("A disputed review", row.GetProperty("post_label").GetString());
    Assert.Equal("a-disputed-review", row.GetProperty("post_slug").GetString());
    Assert.False(row.GetProperty("has_ai_draft").GetBoolean());
    Assert.False(row.GetProperty("has_draft").GetBoolean());
    AssertPrivateDetailAbsent(row);
    Assert.False(row.TryGetProperty("claim_text", out _));
    var decoded = encoded.Deserialize<ModerationDisputeListResponse>(options)!;
    Assert.Equal(summary.PostContent, Assert.Single(decoded.Disputes).PostContent);
    Assert.Equal(page.PageInfo.StartCursor, decoded.PageInfo.StartCursor);
    Assert.False(decoded.PageInfo.HasMorePages);
    Assert.Null(decoded.Disputes[0].AiDraftRequestedRunId);
  }

  private static JsonSerializerOptions Options(bool includeNulls) => new(VouchaApiJson.Options)
  {
    DefaultIgnoreCondition = includeNulls ? JsonIgnoreCondition.Never : JsonIgnoreCondition.WhenWritingNull,
  };

  private static void AssertPrivateDetailAbsent(JsonElement row)
  {
    foreach (var field in new[]
    {
      "ai_draft_requested_run_id", "ai_draft_run_id", "ai_public_response", "ai_internal_response",
      "model", "staff_context", "internal_notes", "edited_by_id", "approved_by_id", "resolved_by_id",
    })
      Assert.False(row.TryGetProperty(field, out _), $"A list summary must omit private detail field {field}.");
  }
}
