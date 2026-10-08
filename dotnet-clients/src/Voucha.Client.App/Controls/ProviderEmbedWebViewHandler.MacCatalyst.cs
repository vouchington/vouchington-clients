#if MACCATALYST
using CoreGraphics;
using Microsoft.Maui.Handlers;
using WebKit;

namespace Voucha.Client.App.Controls;

/// <summary>Creates the player view with an ephemeral website data store before native construction.</summary>
public sealed class ProviderEmbedWebViewHandler : WebViewHandler
{
  protected override WKWebView CreatePlatformView()
  {
    var configuration = new WKWebViewConfiguration
    {
      WebsiteDataStore = WKWebsiteDataStore.NonPersistentDataStore,
    };
    return new WKWebView(CGRect.Empty, configuration);
  }

  protected override void DisconnectHandler(WKWebView platformView)
  {
    platformView.StopLoading();
    platformView.NavigationDelegate = null!;
    platformView.UIDelegate = null!;
    base.DisconnectHandler(platformView);
    platformView.Dispose();
  }
}
#endif
