using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiClientModerationTransparencyTests
{
  [Fact]
  public async Task FetchModerationTransparencyUsesThePaidProjectionRoute()
  {
    var handler = new RecordingHandler([
      new RecordedResponse(ApiFixtureLoader.LoadResponse("native.moderation.transparency.default")),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchModerationTransparencyAsync(
        "all",
        "older-page",
        TestContext.Current.CancellationToken);

    Assert.Equal(
        "/api/v1/moderation-transparency?after=older-page&range=all",
        handler.Requests[0].PathAndQuery);
    Assert.Equal(25, response.Buckets[0].Count);
  }
}
