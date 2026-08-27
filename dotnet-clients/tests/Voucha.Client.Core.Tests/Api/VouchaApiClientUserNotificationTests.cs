using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiClientTests
{
  [Fact]
  public async Task FetchNotificationsAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("swift.notifications.default");

    var response = await client.FetchNotificationsAsync(TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/notifications?limit=20");
    Assert.Equal("n1", response.Results[0].Id);
    Assert.Equal("post", response.Notifications["n1"].EntityType);
    Assert.Equal("/discussion/fixture-post", response.Notifications["n1"].TargetPath);
    Assert.Equal("p1", response.Notifications["n1"].PostId);
    Assert.Null(response.Results[0].ReadAt);
    Assert.Null(response.Notifications["n1"].ReadAt);
    Assert.Equal("Test Community", response.Communities?["community-1"].Name);
  }

  [Fact]
  public async Task FetchNotificationsAsyncForwardsCursorAndLimit()
  {
    var (client, handler) = CreateClient("swift.notifications.default");

    await client.FetchNotificationsAsync(
        new FetchNotificationsRequest("cursor-1", 7),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/notifications?after=cursor-1&limit=7");
  }

  [Fact]
  public async Task MarkNotificationReadAsyncUsesPatchEndpoint()
  {
    var handler = new RecordingHandler("{}");
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    await client.MarkNotificationReadAsync("notification 1", TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Patch, "/api/v1/my/notifications/notification%201");
  }

  [Fact]
  public async Task FetchNotificationRedirectTargetAsyncUsesRedirectTargetEndpoint()
  {
    var handler = new RecordingHandler("""{"target_url":"/rss-feed-items/item-1"}""");
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    var response = await client.FetchNotificationRedirectTargetAsync(
        "notification 1",
        TestContext.Current.CancellationToken);

    Assert.Equal("/rss-feed-items/item-1", response.TargetUrl);
    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/notifications/notification%201/redirect-target");
  }

  [Fact]
  public async Task MarkAllNotificationsReadAsyncUsesReadAllEndpoint()
  {
    var handler = new RecordingHandler("{}");
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    await client.MarkAllNotificationsReadAsync(TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Post, "/api/v1/my/notifications/read-all");
  }

  [Fact]
  public async Task FetchUserFollowingAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("swift.users.following.default");

    var response = await client.FetchUserFollowingAsync(
        new FetchUserFollowingRequest("user-abc"),
        TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/users/user-abc/users/following?limit=100");
    Assert.Equal("friend", response.Results[0].Username);
  }

  [Fact]
  public async Task FetchUserAsyncCanIncludeBioAndProfileMetrics()
  {
    var (client, handler) = CreateClient("native.users.profile.default");

    var response = await client.FetchUserAsync(
        "alice",
        includeBio: true,
        cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/users/alice?include_bio=1");
    Assert.Equal("avatar-1", response.User.ProfileImageId);
    Assert.Null(response.User.DisplayAccount);
    Assert.Equal(4, response.UserMetrics?.Count.Reviews);
    Assert.Equal(7, response.UserMetrics?.ViewerCount?.Comments);
    Assert.Equal("Site", response.ProfileLinks?.Single().Name);
    Assert.Equal("<p>Hello</p>", response.UserBioHtml);
  }

  [Fact]
  public async Task FetchRestrictedUserAsyncReceivesViewerFilteredCollectionCounts()
  {
    var (client, handler) = CreateClient("native.users.profile.restricted");

    var response = await client.FetchUserAsync(
        "restricted",
        includeBio: true,
        cancellationToken: TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/users/restricted?include_bio=1");
    Assert.Equal(0, response.UserMetrics?.Count.UsersFollowing);
    Assert.Equal(0, response.UserMetrics?.Count.UsersFollowers);
    Assert.Equal(0, response.UserMetrics?.Count.TopicsFollowing);
    Assert.Equal(0, response.UserMetrics?.Count.RssFeedsFollowing);
    Assert.Equal(0, response.UserMetrics?.Bookmarks?["follow"]["topics"]);
    Assert.Equal(0, response.UserMetrics?.Bookmarks?["follow"]["posts"]);
    Assert.Equal(0, response.UserMetrics?.Bookmarks?["follow"]["users"]);
    Assert.Equal(0, response.UserMetrics?.Bookmarkers?["follow"]);
  }

  [Fact]
  public async Task FetchMyIdentityAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("swift.my.identity.default");

    var response = await client.FetchMyIdentityAsync(TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/identity");
    Assert.Equal("alice", response.Identity.Username);
    Assert.Null(response.Identity.MembershipPlan);
    Assert.Contains("user", Assert.IsAssignableFrom<IReadOnlyList<string>>(response.Identity.Roles));
  }

  [Fact]
  public async Task EmailPreferencesMethodsUseTheTypedSettingsEndpoint()
  {
    var (client, handler) = CreateClient(
        "native.my.email-preferences.default",
        "native.my.email-preferences.default");

    var fetched = await client.FetchEmailPreferencesAsync(TestContext.Current.CancellationToken);
    Assert.Equal("weekly", fetched.EmailPreferences.NewsDigestFrequency);
    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/email-preferences");

    var updated = await client.UpdateEmailPreferencesAsync(
        new UpdateEmailPreferencesBody(NewsDigestFrequency: "daily"),
        TestContext.Current.CancellationToken);
    Assert.Equal("weekly", updated.EmailPreferences.CommunityDigestFrequency);
    AssertRequest(handler, HttpMethod.Patch, "/api/v1/my/email-preferences");
    Assert.Contains("\"news_digest_frequency\":\"daily\"", handler.RequestBody, StringComparison.Ordinal);
  }

  [Fact]
  public async Task FetchMyProfileAsyncUsesSharedFixture()
  {
    var (client, handler) = CreateClient("swift.my.profile.default");

    var response = await client.FetchMyProfileAsync(TestContext.Current.CancellationToken);

    AssertRequest(handler, HttpMethod.Get, "/api/v1/my/profile");
    Assert.Equal("Hello world", response.Profile.Markdown);
  }
}
