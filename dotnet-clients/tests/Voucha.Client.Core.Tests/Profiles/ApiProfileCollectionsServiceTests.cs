using Voucha.Client.Core.Api;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed class ApiProfileCollectionsServiceTests
{
  [Fact]
  public void ConstructorRejectsNullClient()
  {
    Assert.Throws<ArgumentNullException>(() => new ApiProfileCollectionsService(null!));
  }

  [Fact]
  public async Task FetchMethodsMapProfileScopesAndPaginationToApiRequests()
  {
    const string emptyPage =
        """{"results":[],"page_info":{"end_cursor":"next","has_next_page":true,"start_cursor":"first"}}""";
    var handler = new RecordingHandler(Enumerable.Repeat(new RecordedResponse(emptyPage), 5));
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var service = new ApiProfileCollectionsService(client);
    var cancellationToken = TestContext.Current.CancellationToken;

    var topics = await service.FetchTopicsAsync("user 1", "cursor 1", cancellationToken);
    var following = await service.FetchFollowingAsync("user 1", "cursor 1", cancellationToken);
    var followers = await service.FetchFollowersAsync("user 1", "cursor 1", cancellationToken);
    var sources = await service.FetchSourcesAsync("user 1", "podcast", "cursor 1", cancellationToken);
    var communities = await service.FetchCommunitiesAsync("user 1", "cursor 1", cancellationToken);

    Assert.All(
        new[] { topics.PageInfo, following.PageInfo, followers.PageInfo, sources.PageInfo, communities.PageInfo },
        pageInfo =>
        {
          Assert.Equal("next", pageInfo.EndCursor);
          Assert.True(pageInfo.HasNextPage);
        });
    Assert.Equal(
        [
          "/api/v1/users/user%201/topics/following?after=cursor%201&limit=25",
          "/api/v1/users/user%201/users/following?after=cursor%201&limit=25",
          "/api/v1/users/user%201/users/followers?after=cursor%201&limit=25",
          "/api/v1/users/user%201/rss-feeds/following?after=cursor%201&feed_type=podcast&limit=25",
          "/api/v1/users/user%201/communities/member?after=cursor%201&limit=25",
        ],
        handler.Requests.Select(request => request.PathAndQuery));
  }
}
