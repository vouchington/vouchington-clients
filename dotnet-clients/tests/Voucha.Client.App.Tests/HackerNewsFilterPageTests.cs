using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class HackerNewsFilterPageTests
{
  [Fact]
  public void PostAndNewsPagesRenderBoundNativeHackerNewsFilters()
  {
    var posts = AppSource.Read("Pages", "PostsPage.xaml");
    var news = AppSource.Read("Pages", "NewsFeedsPage.xaml");

    Assert.Contains("Text=\"{Binding HackerNewsFilterLabel}\"", posts, StringComparison.Ordinal);
    Assert.Contains("Clicked=\"OnHackerNewsClicked\"", posts, StringComparison.Ordinal);
    Assert.Contains("Text=\"{Binding HackerNewsFilterLabel}\"", news, StringComparison.Ordinal);
    Assert.Contains("Clicked=\"OnHackerNewsFilterClicked\"", news, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding IsHackerNewsFilterVisible}\"", news, StringComparison.Ordinal);
    Assert.Contains("Binding IsHackerNewsFilterSelected", posts, StringComparison.Ordinal);
    Assert.Contains("Binding IsHackerNewsFilterSelected", news, StringComparison.Ordinal);
  }
}
