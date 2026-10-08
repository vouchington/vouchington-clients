#if MACCATALYST
using Foundation;
using WebKit;
using Voucha.Client.Core.Content;

namespace Voucha.Client.App.Controls;

public sealed partial class ProviderEmbedWebView
{
  private WKWebView? native;
  private readonly ProviderNavigationDelegate navigationDelegate = new();
  private readonly ProviderUiDelegate uiDelegate = new();

  partial void ConfigurePlatform()
  {
    if (Handler?.PlatformView is not WKWebView candidate || ReferenceEquals(native, candidate)) return;
    TearDownPlatform();
    native = candidate;
    native.NavigationDelegate = navigationDelegate;
    native.UIDelegate = uiDelegate;
    if (playerUrl is { } url) LoadPlatform(url, $"https://{Microsoft.Maui.ApplicationModel.AppInfo.Current.PackageName}/");
  }

  partial void LoadPlatform(Uri url, string referer)
  {
    if (native is null) return;
    using var request = new NSMutableUrlRequest(new NSUrl(url.AbsoluteUri));
    request["Referer"] = referer;
    native.LoadRequest(request);
  }

  partial void TearDownPlatform()
  {
    if (native is null) return;
    native.StopLoading();
    native.NavigationDelegate = null!;
    native.UIDelegate = null!;
    native = null;
  }

  private sealed class ProviderNavigationDelegate : WKNavigationDelegate
  {
    public override void DecidePolicy(
        WKWebView webView,
        WKNavigationAction navigationAction,
        Action<WKNavigationActionPolicy> decisionHandler) =>
        decisionHandler(navigationAction.Request.Url?.AbsoluteString is { } absolute &&
            Uri.TryCreate(absolute, UriKind.Absolute, out var target) &&
            UrlEmbedPreviews.IsApprovedPlayer(target)
            ? WKNavigationActionPolicy.Allow
            : WKNavigationActionPolicy.Cancel);
  }

  private sealed class ProviderUiDelegate : WKUIDelegate
  {
    public override WKWebView? CreateWebView(
        WKWebView webView,
        WKWebViewConfiguration configuration,
        WKNavigationAction navigationAction,
        WKWindowFeatures windowFeatures) => null;
  }
}
#endif
