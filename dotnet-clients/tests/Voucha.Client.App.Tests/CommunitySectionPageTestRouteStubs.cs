using Microsoft.Maui.Controls;

namespace Voucha.Client.App.Pages;

// App.Tests compiles selected production page sources; the full App build supplies these native destinations.
public sealed class PostDetailPage : ContentPage
{
  public PostDetailPage() { }

  public PostDetailPage(
      Voucha.Client.Core.Posts.ICommentThreadService threadService,
      Voucha.Client.Core.Posts.IPostsService postsService,
      Voucha.Client.Core.Auth.ISessionStore sessionStore,
      IServiceProvider serviceProvider,
      string postId) { }
}
public sealed class EmbedPlayerPage(Uri playerUrl, Uri sourceUrl) : ContentPage
{
  public Uri PlayerUrl { get; } = playerUrl;
  public Uri SourceUrl { get; } = sourceUrl;
}
