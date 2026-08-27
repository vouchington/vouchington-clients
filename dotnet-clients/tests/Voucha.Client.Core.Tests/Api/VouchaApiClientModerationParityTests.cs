using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task DisputeLifecycleAndExposureMethodsUseTypedRequests()
  {
    var handler = new RecordingHandler([
      Fixture("native.moderation.disputes.detail.default"),
      Fixture("native.moderation.disputes.update.default"),
      Fixture("native.moderation.disputes.approval.default"),
      Fixture("native.moderation.disputes.delivery.default"),
      Fixture("native.moderation.disputes.resolution.annotate"),
      Fixture("native.moderation.disputes.resolution-drafts.default"),
      Fixture("native.moderation.exposure.default"),
      Fixture("native.moderation.reveals.default"),
    ]);
    var client = new VouchaApiClient(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var token = TestContext.Current.CancellationToken;

    await client.FetchDisputeAsync("dispute-1", token);
    await client.UpdateDisputeAsync("dispute-1", "Public response", "Internal notes", token);
    await client.ApproveDisputeAsync("dispute-1", token);
    await client.DeliverDisputeAsync("dispute-1", token);
    await client.ResolveDisputeAsync(
        "dispute-1",
        ModerationDisputeResolutionAction.Annotate,
        "Visible annotation",
        token);
    var rerun = await client.RerunDisputeResolutionDraftAsync("dispute-1", token);
    var exposure = await client.FetchModerationExposureAsync(token);
    var reveal = await client.RecordModerationRevealAsync(
        "post-1",
        null,
        ModerationRevealSurface.ReviewQueue,
        token);

    Assert.Equal("/api/v1/disputes/dispute-1", handler.Requests[0].PathAndQuery);
    Assert.Equal(
        """{"public_response":"Public response","internal_notes":"Internal notes"}""",
        handler.Requests[1].Body);
    Assert.Equal("/api/v1/disputes/dispute-1/approval", handler.Requests[2].PathAndQuery);
    Assert.Equal("/api/v1/disputes/dispute-1/delivery", handler.Requests[3].PathAndQuery);
    Assert.Equal(
        """{"action":"annotate","body_text":"Visible annotation"}""",
        handler.Requests[4].Body);
    Assert.Equal(
        "/api/v1/disputes/dispute-1/resolution-drafts",
        handler.Requests[5].PathAndQuery);
    Assert.Equal("/api/v1/moderation/exposure", handler.Requests[6].PathAndQuery);
    Assert.Equal("""{"postId":"post-1","surface":"review_queue"}""", handler.Requests[7].Body);
    Assert.True(rerun.Queued);
    Assert.Equal(1, exposure.Exposure.Count);
    Assert.Equal(1, reveal.Exposure.Count);
  }

  private static RecordedResponse Fixture(string id) =>
      new(ApiFixtureLoader.LoadResponse(id));
}
