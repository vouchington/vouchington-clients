using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.Tests.Search;

public sealed partial class OmnisearchViewModelTests
{
  private const string CombinedSearchJson = """
      {
        "topics": [{ "id": "topic-1", "name": "Native Swift", "slug": "native-swift", "topic_type": "topic" }],
        "posts": [{ "id": "post-1", "post_type": "blog_post", "title": "Native post" }],
        "news": [{ "id": "news-1", "url": "https://example.com/native", "title": "Native result", "feed_title": "Example Feed" }],
        "domains": [{ "id": "domain-1", "hostname": "example.com" }],
        "communities": [
          { "id": "community-1", "name": "Open Club", "slug": "open-club", "bookmarked": false },
          { "id": "community-2", "name": "Saved Club", "slug": "saved-club", "bookmarked": true },
          { "id": "community-3", "name": "Alpha Club", "slug": "alpha-club", "bookmarked": true }
        ]
      }
      """;

  private static MutableNavigationViewerProvider AuthenticatedViewerProvider()
  {
    var viewerProvider = new MutableNavigationViewerProvider();
    viewerProvider.SetViewer(new NavigationViewer(true, []));
    return viewerProvider;
  }

  private static MutableNavigationViewerProvider AdministratorViewerProvider()
  {
    var viewerProvider = new MutableNavigationViewerProvider();
    viewerProvider.SetViewer(new NavigationViewer(true, ["administrator"]));
    return viewerProvider;
  }

  private sealed class VotingSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public SessionSnapshot Current { get; private set; } = new(new User("user-1", "alice"));

    public void SetSession(SessionSnapshot snapshot)
    {
      Current = snapshot;
      SessionChanged?.Invoke(this, new SessionChangedEventArgs(snapshot));
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
      SetSession(SessionSnapshot.Anonymous);
      return Task.CompletedTask;
    }
  }

  private sealed class EnglishDeviceLanguageProvider : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["en"];
  }
}
