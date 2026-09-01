using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Content;

namespace Voucha.Client.App.Controls;

/// <summary>Accepted narrow WebView exception for backend-approved player URLs only.</summary>
public sealed partial class ProviderEmbedWebView : WebView
{
  private Uri? playerUrl;

  public ProviderEmbedWebView()
  {
    HeightRequest = 200;
    Navigating += OnNavigating;
    HandlerChanged += (_, _) => ConfigurePlatform();
    HandlerChanging += (_, _) => TearDownPlatform();
  }

  public void Load(Uri url)
  {
    if (!UrlEmbedPreviews.IsApprovedPlayer(url)) throw new ArgumentException("Unsupported player URL.", nameof(url));
    playerUrl = url;
    ConfigurePlatform();
    LoadPlatform(url, $"https://{AppInfo.Current.PackageName}/");
  }

  public void Unload()
  {
    playerUrl = null;
    TearDownPlatform();
    Handler?.DisconnectHandler();
  }

  private void OnNavigating(object? sender, WebNavigatingEventArgs args)
  {
    if (!Uri.TryCreate(args.Url, UriKind.Absolute, out var target) || !UrlEmbedPreviews.IsApprovedPlayer(target)) args.Cancel = true;
  }

  partial void ConfigurePlatform();
  partial void TearDownPlatform();
  partial void LoadPlatform(Uri url, string referer);
}
