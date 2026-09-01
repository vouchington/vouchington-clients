#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Voucha.Client.Core.Content;

namespace Voucha.Client.App.Controls;

public sealed partial class ProviderEmbedWebView
{
  private CoreWebView2? core;
  private WebView2? native;

  partial void ConfigurePlatform()
  {
    if (Handler?.PlatformView is not WebView2 candidate) return;
    native?.CoreWebView2Initialized -= OnCoreInitialized;
    native = candidate;
    native.CoreWebView2Initialized -= OnCoreInitialized;
    native.CoreWebView2Initialized += OnCoreInitialized;
    ConfigureCore(native.CoreWebView2);
  }

  partial void LoadPlatform(Uri url, string referer)
  {
    if (core is null) return;
    var headers = $"Referer: {referer}\r\n";
    var request = core.Environment.CreateWebResourceRequest(url.AbsoluteUri, "GET", null, headers);
    core.NavigateWithWebResourceRequest(request);
  }

  partial void TearDownPlatform()
  {
    if (core is not null)
    {
      core.NavigationStarting -= OnNavigationStarting;
      core.NewWindowRequested -= OnNewWindowRequested;
    }
    core = null;
    native?.CoreWebView2Initialized -= OnCoreInitialized;
    native = null;
  }

  private void OnCoreInitialized(WebView2 sender, CoreWebView2InitializedEventArgs args) => ConfigureCore(sender.CoreWebView2);

  private void ConfigureCore(CoreWebView2? candidate)
  {
    if (candidate is null || ReferenceEquals(core, candidate)) return;
    TearDownPlatform();
    core = candidate;
    core.Settings.IsWebMessageEnabled = false;
    core.NavigationStarting += OnNavigationStarting;
    core.NewWindowRequested += OnNewWindowRequested;
    if (playerUrl is { } url) LoadPlatform(url, $"https://{Microsoft.Maui.ApplicationModel.AppInfo.Current.PackageName}/");
  }

  private static void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs args) => args.Handled = true;

  private static void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs args)
  {
    if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var target) || !UrlEmbedPreviews.IsApprovedPlayer(target)) args.Cancel = true;
  }
}
#endif
