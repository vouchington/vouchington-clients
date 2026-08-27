using Voucha.Client.Core.Api;
using Voucha.Client.Core.Notifications;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Notifications;

public sealed class ApiNotificationsServiceTests
{
  [Fact]
  public void ConstructorRejectsNullClient()
  {
    Assert.Throws<ArgumentNullException>(() => new ApiNotificationsService(null!));
  }

  [Fact]
  public async Task FetchAsyncDelegatesToNotificationsClient()
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse("swift.notifications.default"));
    var service = new ApiNotificationsService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var response = await service.FetchAsync(
        new FetchNotificationsRequest("cursor-1", 7),
        TestContext.Current.CancellationToken);

    Assert.Equal("n1", response.Results[0].Id);
    Assert.Equal("/api/v1/my/notifications?after=cursor-1&limit=7", handler.PathAndQuery);
  }

  [Fact]
  public async Task MarkReadAsyncDelegatesToNotificationsClient()
  {
    var handler = new RecordingHandler("{}");
    var service = new ApiNotificationsService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    await service.MarkReadAsync("notification-1", TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Patch, handler.Method);
    Assert.Equal("/api/v1/my/notifications/notification-1", handler.PathAndQuery);
  }

  [Fact]
  public async Task FetchRedirectTargetAsyncDelegatesToNotificationsClient()
  {
    var handler = new RecordingHandler("""{"target_url":"/rss-feed-items/item-1"}""");
    var service = new ApiNotificationsService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var response = await service.FetchRedirectTargetAsync("notification 1", TestContext.Current.CancellationToken);

    Assert.Equal("/rss-feed-items/item-1", response.TargetUrl);
    Assert.Equal(HttpMethod.Get, handler.Method);
    Assert.Equal("/api/v1/my/notifications/notification%201/redirect-target", handler.PathAndQuery);
  }

  [Fact]
  public async Task MarkAllReadAsyncDelegatesToNotificationsClient()
  {
    var handler = new RecordingHandler("{}");
    var service = new ApiNotificationsService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    await service.MarkAllReadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/my/notifications/read-all", handler.PathAndQuery);
  }
}
