using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class TopicClaimEndpointTests
{
  [Fact]
  public void TopicClaimListsForwardTheOpaqueCursor()
  {
    var mine = VouchaApiEndpoints.MyTopicClaims(after: "cursor-1", limit: 1);
    Assert.Equal("/api/v1/my/topic-claims", mine.Path);
    Assert.Equal("cursor-1", mine.Query["after"]);
    Assert.Equal("1", mine.Query["limit"]);

    var topic = VouchaApiEndpoints.TopicClaims(
        "topic-1",
        mineOnly: true,
        activeOnly: false,
        after: "cursor-2",
        limit: 2);
    Assert.Equal("/api/v1/topics/topic-1/claims", topic.Path);
    Assert.Equal("cursor-2", topic.Query["after"]);
    Assert.Equal("true", topic.Query["mine_only"]);
    Assert.Equal("false", topic.Query["active_only"]);
    Assert.Equal("2", topic.Query["limit"]);
  }
}
