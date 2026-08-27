using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

internal static partial class ApiFixtureCoverage
{
  private static Dictionary<string, ApiRequest> WithFollowerDistributionEndpoints(
      this Dictionary<string, ApiRequest> registry)
  {
    var recipient = "01900000-0000-7000-8000-000000000502";
    registry["native.users.followers.search"] = VouchaApiEndpoints.UserFollowers("user-abc", 25, query: "al");
    registry["native.posts.followers.share"] = VouchaApiEndpoints.SharePostWithFollowers("post-abc");
    registry["native.posts.followers.send-selected"] = VouchaApiEndpoints.SendPostToFollowers("post-abc", FollowerDistributionBody.Selected([recipient]));
    registry["native.rss-feed-items.followers.share"] = VouchaApiEndpoints.ShareRssFeedItemWithFollowers("01900000-0000-7000-8000-000000000503");
    registry["native.rss-feed-items.followers.send-selected"] = VouchaApiEndpoints.SendRssFeedItemToFollowers("01900000-0000-7000-8000-000000000503", FollowerDistributionBody.Selected([recipient]));
    return registry;
  }
}
