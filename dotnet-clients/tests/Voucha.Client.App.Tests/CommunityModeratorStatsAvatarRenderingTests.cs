using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityModeratorStatsAvatarRenderingTests
{
  [Fact]
  public async Task ModeratorStatsRowsRenderOnlyTheResolvedPublicUserPlacement()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var responses = new Dictionary<string, string>
    {
      ["/api/v1/communities/test-community"] = Fixture("web.communities.show.default"),
      ["/api/v1/communities/test-community/members"] = Fixture("web.communities.members.default"),
      ["/api/v1/communities/test-community/posts"] = Fixture("web.communities.posts.default"),
      ["/api/v1/communities/test-community/list-items/counts"] = Fixture("web.communities.list-items.counts.default"),
      ["/api/v1/communities/test-community/moderator-stats"] = ModeratorStatsFixtureWithMemberPlacement(),
      ["/api/v1/communities/test-community/moderation-queue"] = Fixture("web.communities.moderation-queue.default"),
      ["/api/v1/communities/test-community/reports/pending"] = Fixture("native.community.pending-reports.paginated"),
    };
    var apiClient = new VouchaApiClient(new HttpClient(new RouteResponseHandler(responses))
    {
      BaseAddress = new Uri("https://api.test"),
    });
    using var model = new CommunityDetailViewModel(new ApiCommunitiesService(apiClient));
    var page = new CommunityModerationPage(
        model,
        new AnonymousSessionStore(),
        new ServiceCollection().BuildServiceProvider(),
        new AppConfig(new Uri("https://api.test"), ImageBaseUrl: new Uri("https://images.test")));

    await model.LoadSurfaceAsync(
        "test-community",
        CommunityDetailSurfaceSection.Moderation,
        TestContext.Current.CancellationToken);

    var stat = Assert.Single(model.Moderation, row => row.Id == "user-1");
    Assert.Equal("testuser", stat.Title);
    Assert.Equal("3", stat.Detail);
    Assert.Equal("00000000-0000-7000-8000-000000000301", stat.ProfileImagePlacement?.PlacementId);
    var collection = Assert.IsType<CollectionView>(typeof(CommunitySectionPage)
        .GetField("rowsView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .GetValue(page));
    var template = Assert.IsType<HorizontalStackLayout>(collection.ItemTemplate!.CreateContent());
    template.BindingContext = stat;
    var avatar = Assert.Single(Descendants<Border>(template), border => border.AutomationId == "community-moderator-avatar");
    var image = Assert.Single(Descendants<Image>(avatar), candidate => candidate.AutomationId == "community-moderator-avatar-image");

    Assert.True(avatar.IsVisible);
    Assert.False(AutomationProperties.GetIsInAccessibleTree(image));
    Assert.Equal(new Uri("https://images.test/images/placements/00000000-0000-7000-8000-000000000301/2/00000000-0000-7000-8000-000000000201?w=96"),
        Assert.IsType<UriImageSource>(image.Source).Uri);

    template.BindingContext = stat with { ProfileImagePlacement = null };
    Assert.False(avatar.IsVisible);
    Assert.Null(image.Source);
  }

  private static string Fixture(string id)
  {
    var manifest = JsonNode.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("manifest.json")))!;
    var path = manifest["fixtures"]!.AsArray().Single(entry => (string?)entry?["id"] == id)!["bodyFile"]!.GetValue<string>();
    return File.ReadAllText(FilamentsContractPaths.ApiFixture(path));
  }

  private static string ModeratorStatsFixtureWithMemberPlacement()
  {
    var stats = JsonNode.Parse(Fixture("web.communities.moderator-stats.default"))!;
    var members = JsonNode.Parse(Fixture("web.communities.members.default"))!;
    stats["users"]!["user-1"]!["profile_image_placement"] =
        members["users"]!["user-1"]!["profile_image_placement"]!.DeepClone();
    return stats.ToJsonString();
  }

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private sealed class RouteResponseHandler(IReadOnlyDictionary<string, string> responses) : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var path = request.RequestUri?.AbsolutePath ?? "";
      if (!responses.TryGetValue(path, out var body))
      {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
          RequestMessage = request,
        });
      }
      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
        RequestMessage = request,
      });
    }
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
