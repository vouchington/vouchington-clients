using Voucha.Client.App.Pages;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private PostsPage CreatePostsPage(NativeRouteMatch? match)
  {
    var page = serviceProvider.GetRequiredService<PostsPage>();
    if (PostBrowseRoute.FromMatch(match) is { } scope) page.ConfigureBrowseScope(scope);
    return page;
  }

  /// <summary>Applies a post route to an already-open Posts page; returns true when the match must be stored.</summary>
  private async Task<bool> TryStorePostsRouteMatchAsync(NativeRouteMatch? match)
  {
    var scope = PostBrowseRoute.FromMatch(match);
    var isFeed = match?.Path.StartsWith("/feed", StringComparison.Ordinal) == true;
    if (scope is null && !isFeed) return true;
    foreach (var page in EnumerateShellContentPages("posts"))
    {
      if (page is PostsPage postsPage)
      {
        await (scope is null ? postsPage.ApplyFeedModeAsync() : postsPage.ApplyBrowseScopeAsync(scope)).ConfigureAwait(true);
        return false;
      }
    }
    return true;
  }

  private async Task OpenTopicPostsAsync(TopicPostsRoute route)
  {
    var scope = await route.ResolveRequestAsync(
        serviceProvider.GetRequiredService<ITopicsService>(),
        serviceProvider.GetRequiredService<ISessionStore>().Current.IsAuthenticated).ConfigureAwait(true);
    var page = serviceProvider.GetRequiredService<PostsPage>();
    page.ConfigureBrowseScope(scope, topicScope: true);
    await Navigation.PushAsync(page).ConfigureAwait(true);
  }
}
