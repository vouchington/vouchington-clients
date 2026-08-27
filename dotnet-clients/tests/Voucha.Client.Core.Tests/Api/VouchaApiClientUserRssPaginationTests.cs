using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchUserRssFeedsAsyncPropagatesScopeAndCursor()
  {
    var handler = new RecordingHandler("""{"results":[],"page_info":{"has_next_page":false}}""");
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    await client.FetchUserRssFeedsAsync(
        new FetchUserRssFeedsRequest("user 1", FeedType: "podcast", After: "cursor 2", Limit: 8),
        TestContext.Current.CancellationToken);

    AssertRequest(
        handler,
        HttpMethod.Get,
        "/api/v1/users/user%201/rss-feeds/following?after=cursor%202&feed_type=podcast&limit=8");
  }
}
