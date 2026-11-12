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
