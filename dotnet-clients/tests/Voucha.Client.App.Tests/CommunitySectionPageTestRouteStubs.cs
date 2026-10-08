using Microsoft.Maui.Controls;

namespace Voucha.Client.App.Pages;

// App.Tests compiles selected production page sources; the full App build supplies these native destinations.
public sealed class PostDetailPage : ContentPage { }
public sealed class EmbedPlayerPage(Uri playerUrl, Uri sourceUrl) : ContentPage
{
  public Uri PlayerUrl { get; } = playerUrl;
  public Uri SourceUrl { get; } = sourceUrl;
}
