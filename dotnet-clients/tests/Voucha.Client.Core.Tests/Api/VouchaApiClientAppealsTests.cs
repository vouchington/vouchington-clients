using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task AppealLifecycleMethodsUseTypedRequestsAndDeserializeCanonicalResponses()
  {
    var envelope = """{"appeal":{"id":"appeal-1","status":"pending","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z"}}""";
    var handler = new RecordingHandler([
      new RecordedResponse(envelope),
      new RecordedResponse(envelope),
      new RecordedResponse(envelope),
      new RecordedResponse(envelope),
      new RecordedResponse(envelope),
      new RecordedResponse("""{"queued":true,"rerun_by_id":"staff-1"}"""),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var token = TestContext.Current.CancellationToken;

    await client.FetchAppealAsync("appeal-1", token);
    await client.UpdateAppealPublicResponseAsync("appeal-1", "Reviewed", token);
    await client.ApproveAppealAsync("appeal-1", token);
    await client.DeliverAppealAsync("appeal-1", token);
    await client.ResolveAppealAsync("appeal-1", ModerationAppealAction.Reduce, token);
    var queued = await client.RerunAppealResolutionDraftAsync("appeal-1", token);

    Assert.Equal("/api/v1/appeals/appeal-1", handler.Requests[0].PathAndQuery);
    Assert.Equal("""{"public_response":"Reviewed"}""", handler.Requests[1].Body);
    Assert.Equal("/api/v1/appeals/appeal-1/approval", handler.Requests[2].PathAndQuery);
    Assert.Equal("/api/v1/appeals/appeal-1/delivery", handler.Requests[3].PathAndQuery);
    Assert.Equal("""{"action":"reduce"}""", handler.Requests[4].Body);
    Assert.Equal("/api/v1/appeals/appeal-1/resolution-drafts", handler.Requests[5].PathAndQuery);
    Assert.True(queued.Queued);
    Assert.Equal("staff-1", queued.RerunById);
  }
}
