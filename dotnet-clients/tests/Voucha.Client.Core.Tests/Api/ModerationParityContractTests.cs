using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ModerationParityContractTests
{
  [Fact]
  public void AppealContextsDecodeAndRemainOptionalForOldResponses()
  {
    var json = ApiFixtureLoader.LoadResponse("native.moderation.appeals.default");
    var response = JsonSerializer.Deserialize<ModerationAppealListResponse>(json, VouchaApiJson.Options);
    var appeal = Assert.Single(Assert.IsType<ModerationAppealListResponse>(response).Appeals);

    var target = Assert.IsType<ModerationAppealPostRemovalContext>(appeal.TargetContext);
    Assert.Equal("A removed review", target.Title);
    Assert.Equal("appealing-reviewer", appeal.StaffContext?.Appellant.Username);
    Assert.Equal("moderator", appeal.StaffContext?.OriginalDecision.Actor?.Username);

    var oldShape = RemoveAggregateKey(json, "appeals", "target_context", "staff_context");
    var oldResponse = JsonSerializer.Deserialize<ModerationAppealListResponse>(
        oldShape,
        VouchaApiJson.Options);
    var oldAppeal = Assert.Single(Assert.IsType<ModerationAppealListResponse>(oldResponse).Appeals);
    Assert.Null(oldAppeal.TargetContext);
    Assert.Null(oldAppeal.StaffContext);
  }

  [Fact]
  public void DisputeContextsAndLifecycleEnvelopesDecode()
  {
    var json = ApiFixtureLoader.LoadResponse("native.moderation.disputes.default");
    var response = JsonSerializer.Deserialize<ModerationDisputeListResponse>(json, VouchaApiJson.Options);
    var dispute = Assert.Single(Assert.IsType<ModerationDisputeListResponse>(response).Disputes);

    Assert.Equal("native-claimant", dispute.StaffContext?.Disputant.Username);
    Assert.Equal("A disputed review", dispute.StaffContext?.Review.Post.Title);
    Assert.Equal("Native topic", dispute.StaffContext?.Review.Topic?.Name);
    Assert.Equal(1, dispute.StaffContext?.Review.Rating);

    var envelopeFixtures = new[]
    {
      "native.moderation.disputes.detail.default",
      "native.moderation.disputes.update.default",
      "native.moderation.disputes.approval.default",
      "native.moderation.disputes.delivery.default",
      "native.moderation.disputes.resolution.remove",
      "native.moderation.disputes.resolution.annotate",
      "native.moderation.disputes.resolution.dismiss",
    };
    foreach (var fixtureId in envelopeFixtures)
    {
      var envelope = JsonSerializer.Deserialize<ModerationDisputeResponse>(
          ApiFixtureLoader.LoadResponse(fixtureId),
          VouchaApiJson.Options);
      Assert.NotNull(envelope?.Dispute.StaffContext);
    }

    var rerun = JsonSerializer.Deserialize<ModerationDisputeQueueResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.resolution-drafts.default"),
        VouchaApiJson.Options);
    Assert.True(rerun?.Queued);
    Assert.Equal("staff-1", rerun?.RerunById);

    var oldShape = RemoveAggregateKey(json, "disputes", "staff_context");
    var oldResponse = JsonSerializer.Deserialize<ModerationDisputeListResponse>(
        oldShape,
        VouchaApiJson.Options);
    Assert.Null(Assert.Single(Assert.IsType<ModerationDisputeListResponse>(oldResponse).Disputes).StaffContext);
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

  private static string RemoveAggregateKey(
      string json,
      string collectionKey,
      params string[] removedKeys)
  {
    var root = JsonNode.Parse(json)?.AsObject() ??
        throw new InvalidOperationException("Fixture must contain a JSON object.");
    var rows = root[collectionKey]?.AsArray() ??
        throw new InvalidOperationException($"Fixture must contain {collectionKey}.");
    foreach (var row in rows)
    {
      var value = row?.AsObject() ??
          throw new InvalidOperationException($"{collectionKey} must contain objects.");
      foreach (var removedKey in removedKeys)
      {
        value.Remove(removedKey);
      }
    }

    return root.ToJsonString();
  }
}
