using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CommunityModerationPageAutomodNavigationTests
{
  [Fact]
  public async Task MountedModerationPageOpensActualFlagPostThroughNativeRouteCallback()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["UiLocaleVersion"] = 0 } };
    var service = DispatchProxy.Create<ICommunitiesService, CommunityServiceProxy>();
    var session = new SessionStore(new SessionSnapshot(new User(
        Id: "moderator-1", Username: "moderator", Roles: [],
        EmailAddress: "moderator@example.com", MembershipPlan: "free")));
    var model = new CommunityDetailViewModel(service, session);
    var page = new CommunityModerationPage(model, new ServiceCollection().BuildServiceProvider());
    string? openedPost = null;
    page.AutomodPostNavigationOverride = postId => { openedPost = postId; return Task.CompletedTask; };

    await model.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.Moderation, TestContext.Current.CancellationToken);

    var button = Find<Button>(page, "community-automod-open:post-1");
    button.SendClicked();

    Assert.Equal("post-1", openedPost);
    Assert.True(button.IsVisible);
  }

  private static T Find<T>(Element root, string id) where T : Element =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == id);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private sealed class SessionStore(SessionSnapshot current) : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => current;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  public class CommunityServiceProxy : DispatchProxy
  {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
      nameof(ICommunitiesService.FetchDetailAsync) => Task.FromResult(Detail()),
      nameof(ICommunitiesService.FetchMembersAsync) => Task.FromResult(new CommunityMembersResponse([], EmptyPage(),
          new Dictionary<string, CommunityMember>(), new Dictionary<string, PublicUser>())),
      nameof(ICommunitiesService.FetchPostsAsync) => Task.FromResult(new CommunityPostsResponse([], EmptyPage(),
          new Dictionary<string, Post>(), new Dictionary<string, PostMetrics>(), new Dictionary<string, EntityReference>())),
      nameof(ICommunitiesService.FetchListItemCountsAsync) => Task.FromResult(new CommunityListItemCountsResponse(0, 0, 0, 0, 0)),
      nameof(ICommunitiesService.FetchModeratorStatsAsync) => Task.FromResult(new CommunityModeratorStatsResponse(30, [], new Dictionary<string, User>())),
      nameof(ICommunitiesService.FetchModerationQueuePageAsync) => Task.FromResult(new CommunityModerationQueueResponse([], EmptyPage(), "moderator")),
      nameof(ICommunitiesService.FetchPendingReportsPageAsync) => Task.FromResult(new CommunityPendingReportsResponse([], EmptyPage())),
      nameof(ICommunitiesService.FetchAutomodFlagPageAsync) => Task.FromResult(new CommunityModerationQueueResponse([Flag()], EmptyPage(), "moderator")),
      _ => throw new InvalidOperationException($"Unexpected service call: {targetMethod?.Name}"),
    };

    private static PageInfo EmptyPage() => new(null, false, null);

    private static CommunityResponse Detail()
    {
      var community = JsonSerializer.Deserialize<Community>("""
          {"id":"community-1","name":"Community","slug":"community-1","visibility":"public",
          "member_roster_visibility":"members","should_allow_review_posts":true,
          "should_allow_data_point_posts":true,"created_by_id":"moderator-1",
          "created_at":"2026-07-01T00:00:00Z","updated_at":"2026-07-01T00:00:00Z"}
          """, VouchaApiJson.Options)!;
      return new CommunityResponse(community, null, new CommunityMetrics("community-1", 1, 0),
          new CommunityMember("membership-1", "community-1", "moderator-1", "moderator"), false);
    }

    private static CommunityModerationQueueEntry Flag() =>
        new("flag-1", "community-1", "post", "post-1", null, "pending", "automod_flag", null, null, 0,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "Flagged post", "/posts/post-1", true,
            false, false, null, null, null, null, null, null, "/posts/post-1", null, null,
            false, null, null, null);
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
