using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationParityContractTests
{
  [Fact]
  public void StaffAppealsDecodeSummaryWhileDetailRetainsDraftAndStaffContext()
  {
    var list = JsonSerializer.Deserialize<StaffModerationAppealListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.appeals.default"), VouchaApiJson.Options)!;
    var row = Assert.Single(list.Appeals);
    Assert.True(row.HasAiDraft);
    Assert.False(row.HasDraft);
    Assert.Equal("appealing-reviewer", row.Appellant?.Username);
    Assert.Equal("A removed review", Assert.IsType<ModerationAppealPostRemovalContext>(row.TargetContext).Title);
    var encoded = JsonSerializer.SerializeToElement(row, VouchaApiJson.Options);
    Assert.False(encoded.TryGetProperty("staff_context", out _));
    Assert.False(encoded.TryGetProperty("ai_public_response", out _));
    var detail = JsonSerializer.Deserialize<ModerationAppealResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.appeals.detail.default"), VouchaApiJson.Options)!;
    Assert.NotNull(detail.Appeal.StaffContext);
    Assert.NotNull(detail.Appeal.AiPublicResponse);
  }

  [Fact]
  public void StaffDisputesDecodeSummaryWhileDetailRetainsClaim()
  {
    var list = JsonSerializer.Deserialize<StaffModerationDisputeListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.default"), VouchaApiJson.Options)!;
    var row = Assert.Single(list.Disputes);
    Assert.Equal("native-claimant", row.Disputant?.Username);
    Assert.Equal("A disputed review", row.PostContent?.Text);
    Assert.False(row.HasDraft);
    var encoded = JsonSerializer.SerializeToElement(row, VouchaApiJson.Options);
    Assert.False(encoded.TryGetProperty("claim_text", out _));
    Assert.False(encoded.TryGetProperty("staff_context", out _));
    var detail = JsonSerializer.Deserialize<ModerationDisputeResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.detail.default"), VouchaApiJson.Options)!;
    Assert.Equal("Native review dispute", detail.Dispute.ClaimText);
    Assert.NotNull(detail.Dispute.StaffContext);
  }

  [Fact]
  public void ReviewModerationSummaryAndMediaRevealContractsDecode()
  {
    var queueJson = ApiFixtureLoader.LoadResponse("native.moderation.review-queue.default");
    var queue = JsonSerializer.Deserialize<AdminReviewQueueResponse>(queueJson, VouchaApiJson.Options);
    var rows = Assert.IsType<AdminReviewQueueResponse>(queue).Results;
    Assert.False(rows[0].MediaReveal.RequiresReveal);
    Assert.True(rows[1].MediaReveal.RequiresReveal);
    Assert.Equal("019e82f2-a2c0-7000-8000-000000000001", rows[1].MediaReveal.Images[0].ImageId);
    Assert.Equal(AdminModerationDisposition.Review, rows[0].ModerationSummary.Disposition);
    Assert.Equal(1, rows[1].ModerationSummary.EvidenceSummary.FlaggedCategoryCount);
    Assert.Equal(0, rows[1].ModerationSummary.EvidenceSummary.SignalCount);
    Assert.NotEmpty(rows[1].ModerationSummary.ReasonCodes);

    var exposure = JsonSerializer.Deserialize<ModerationExposureResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.exposure.default"),
        VouchaApiJson.Options);
    Assert.Equal(1, exposure?.Exposure.Count);
    Assert.Equal(10, exposure?.Exposure.Threshold);
    Assert.False(exposure?.Exposure.InCooldown);
    Assert.Null(exposure?.Exposure.CooldownEndsAt);

    const string cooldownJson =
        """{"exposure":{"count":10,"threshold":10,"in_cooldown":true,"cooldown_ends_at":"2026-07-29T20:00:00.000Z"}}""";
    var cooldown = JsonSerializer.Deserialize<ModerationExposureResponse>(
        cooldownJson,
        VouchaApiJson.Options);
    Assert.Equal(new DateTimeOffset(2026, 7, 29, 20, 0, 0, TimeSpan.Zero), cooldown?.Exposure.CooldownEndsAt);
  }

}
