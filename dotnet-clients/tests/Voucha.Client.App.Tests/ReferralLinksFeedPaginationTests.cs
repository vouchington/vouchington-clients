using System.Net;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.ReferralLinks;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ReferralLinksFeedPaginationTests
{
  [Fact]
  public async Task RenderedFeedPaginationAppendsRetriesAndHidesAtExhaustion()
  {
    DispatcherProvider.SetCurrent(new ImmediateTimerDispatcherProvider(ImmediateTimerStart.StopAfterTick));
    _ = new Application { Resources = { ["UiLocaleVersion"] = 0 } };
    var handler = new FeedHandler();
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    using var model = new ReferralLinksViewModel(new ApiReferralLinksService(new VouchaApiClient(http)));
    var page = new ReferralLinksPage(model, new Session(), new ReferralLinksRouteContextStore());
    var pagination = Find<HybridPaginationControl>(page, "pagination-referral-feed");
    var button = Find<Button>(page, "pagination-referral-feed-action");
    Assert.False(button.IsVisible);
    await model.LoadFeedAsync(cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(button.IsVisible);
    Assert.True(pagination.HasMore);
    pagination.TryLoadAutomatically();
    await WaitAsync(() => model.HasError);
    Assert.True(pagination.HasError);
    Assert.True(button.IsEnabled);
    Assert.Equal(["first"], model.Items.Select(row => row.Id));
    var failedRequestCount = handler.Paths.Count;
    pagination.TryLoadAutomatically();
    Assert.Equal(failedRequestCount, handler.Paths.Count);
    button.SendClicked();
    await WaitAsync(() => !model.IsLoading && !model.HasError);
    Assert.Equal(["first", "last"], model.Items.Select(row => row.Id));
    Assert.False(button.IsVisible);
    Assert.False(pagination.HasMore);
    Assert.Contains("after=opaque%2F%2B%3F", handler.Paths[1], StringComparison.Ordinal);
    Assert.Equal(handler.Paths[1], handler.Paths[2]);
    Assert.False(Find<HybridPaginationControl>(page, "pagination-referral-links").HasMore);
    Assert.False(Find<HybridPaginationControl>(page, "pagination-referral-analytics").HasMore);
  }

  private static T Find<T>(Element root, string id) where T : Element =>
      Assert.Single(VisualTreeElements.Descendants<T>(root), element => element.AutomationId == id);

  private static async Task WaitAsync(Func<bool> done)
  {
    for (var attempt = 0; attempt < 100 && !done(); attempt++) await Task.Delay(10);
    Assert.True(done());
  }

  private sealed class FeedHandler : HttpMessageHandler
  {
    public List<string> Paths { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Paths.Add(request.RequestUri!.PathAndQuery);
      if (Paths.Count == 2) throw new HttpRequestException("offline");
      var first = Paths.Count == 1;
      var id = first ? "first" : "last";
      var body = new ReferralLinkFeedResponse(
          [new ReferralLinkFeedItem(id, "user", "program", "Program", "program", "https://example.com/ref", id)],
          new Dictionary<string, ReferralLinkFeedUser>(StringComparer.Ordinal),
          new PageInfo(first ? "opaque/+?" : null, first, null));
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(JsonSerializer.Serialize(body, VouchaApiJson.Options)),
      });
    }
  }

  private sealed class Session : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current { get; } = new(new User("user", "member"));
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }
}
