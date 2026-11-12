using System.Reflection;
using Microsoft.Maui.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class TurnstileChallengePageTests
{
  public TurnstileChallengePageTests() =>
      _ = new Application
      {
        Resources =
        {
          ["Headline"] = new Style(typeof(Label)),
          ["Body"] = new Style(typeof(Label)),
        },
      };

  [Fact]
  public void WidgetReceivesMappedActionAndConfiguredWebOrigin()
  {
    var origin = new Uri("https://web.example.test/custom/");
    var page = new TurnstileChallengePage(
        new AppConfig(new Uri("https://api.example.test"), "public-site-key", WebBaseUrl: origin),
        "communities.create-post");
    var webView = page.GetVisualTreeDescendants().OfType<WebView>().Single();
    var source = Assert.IsType<HtmlWebViewSource>(webView.Source);

    Assert.Equal(origin.ToString(), source.BaseUrl);
    Assert.Contains("action: \"communities-create-post\"", source.Html, StringComparison.Ordinal);
    Assert.Contains("sitekey: siteKey", source.Html, StringComparison.Ordinal);
    Assert.Contains("\"expired-callback\"", source.Html, StringComparison.Ordinal);
    Assert.Contains("\"error-callback\"", source.Html, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("close")]
  [InlineData("expired")]
  [InlineData("error")]
  public void ClosingChallengeAllowsFreshPageWithAnotherAction(string callback)
  {
    var config = new AppConfig(new Uri("https://api.example.test"), "public-site-key");
    var first = new TurnstileChallengePage(config, "posts.create");
    var closed = 0;
    var captured = 0;
    first.TokenCaptured += (_, _) => captured++;
    first.CloseRequested += (_, _) => closed++;
    var navigation = new WebNavigatingEventArgs(
        WebNavigationEvent.NewPage, null!, $"voucha-turnstile://{callback}");
    typeof(TurnstileChallengePage)
        .GetMethod("OnNavigating", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(first, [null, navigation]);
    Assert.True(navigation.Cancel);
    Assert.Equal(1, closed);
    var late = new WebNavigatingEventArgs(
        WebNavigationEvent.NewPage, null!, "voucha-turnstile://complete?token=late");
    typeof(TurnstileChallengePage)
        .GetMethod("OnNavigating", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(first, [null, late]);
    Assert.True(late.Cancel);
    Assert.Equal(0, captured);

    var retry = new TurnstileChallengePage(config, "reports.create");
    var html = Assert.IsType<HtmlWebViewSource>(
        retry.GetVisualTreeDescendants().OfType<WebView>().Single().Source).Html;
    Assert.Contains("action: \"reports-create\"", html, StringComparison.Ordinal);
    Assert.DoesNotContain("posts-create", html, StringComparison.Ordinal);
  }

  [Fact]
  public void InvalidActionFailsBeforeSiteKeyResolutionOrWidgetConstruction() =>
      Assert.Throws<InvalidOperationException>(() => new TurnstileChallengePage(
          new AppConfig(new Uri("https://api.example.test")), "posts/create"));

  [Fact]
  public void SiteKeyCannotBreakOutOfInlineScript()
  {
    var page = new TurnstileChallengePage(
        new AppConfig(new Uri("https://api.example.test"), "key</script><script>alert(1)</script>"),
        "posts.create");
    var source = Assert.IsType<HtmlWebViewSource>(
        page.GetVisualTreeDescendants().OfType<WebView>().Single().Source);

    Assert.DoesNotContain("key</script>", source.Html, StringComparison.Ordinal);
    Assert.Contains("\\u003C", source.Html, StringComparison.Ordinal);
  }
}
