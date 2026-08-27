using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  private static (VouchaApiClient Client, RecordingHandler Handler) CreateClient(string fixtureId)
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse(fixtureId));
    return (new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }), handler);
  }

  private static (VouchaApiClient Client, RecordingHandler Handler) CreateClient(params string[] fixtureIds)
  {
    var responses = fixtureIds.Select(id => new RecordedResponse(ApiFixtureLoader.LoadResponse(id)));
    var handler = new RecordingHandler(responses);
    return (new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }), handler);
  }

  private static void AssertRequest(RecordingHandler handler, HttpMethod method, string pathAndQuery)
  {
    Assert.Equal(method, handler.Method);
    Assert.Equal(pathAndQuery, handler.PathAndQuery);
  }
}
