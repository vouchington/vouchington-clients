using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ApiModerationExposureServiceTests
{
  [Fact]
  public async Task ForwardsExposureFetch()
  {
    var (service, handler) = Create("native.moderation.exposure.default");

    var response = await service.FetchExposureAsync(TestContext.Current.CancellationToken);

    var request = Assert.Single(handler.Requests);
    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal("/api/v1/moderation/exposure", request.PathAndQuery);
    Assert.Equal(1, response.Exposure.Count);
  }

  [Fact]
  public async Task RecordsAReviewQueueRevealWithoutAReport()
  {
    var (service, handler) = Create("native.moderation.reveals.default");

    await service.RecordReviewQueueRevealAsync("post-1", TestContext.Current.CancellationToken);

    var request = Assert.Single(handler.Requests);
    Assert.Equal(HttpMethod.Post, request.Method);
    Assert.Equal("/api/v1/moderation/reveals", request.PathAndQuery);
    Assert.Equal("""{"postId":"post-1","surface":"review_queue"}""", request.Body);
  }

  [Theory]
  [InlineData(ModerationDisputeStatus.Pending, "pending")]
  [InlineData(ModerationDisputeStatus.Resolved, "resolved")]
  [InlineData(ModerationDisputeStatus.Dismissed, "dismissed")]
  public async Task ForwardsDisputeStatusAndOpaqueCursorExactly(
      ModerationDisputeStatus status,
      string queryStatus)
  {
    var (service, handler) = Create("native.moderation.disputes.default");
    var disputes = Assert.IsAssignableFrom<IModerationDisputesService>(service);

    await disputes.FetchDisputesAsync(
        status,
        "opaque+cursor/2",
        17,
        TestContext.Current.CancellationToken);

    var request = Assert.Single(handler.Requests);
    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal(
        $"/api/v1/disputes?after=opaque%2Bcursor%2F2&limit=17&status={queryStatus}",
        request.PathAndQuery);
  }

  private static (ApiModerationService Service, RecordingHandler Handler) Create(string fixture)
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse(fixture));
    var client = new VouchaApiClient(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (new ApiModerationService(client), handler);
  }
}
