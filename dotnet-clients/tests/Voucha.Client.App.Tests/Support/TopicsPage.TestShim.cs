using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.FollowerDistributions;

namespace Voucha.Client.App.Pages
{
  public partial class TopicsPage
  {
    private void OnImportExportClicked(object? sender, EventArgs e) { }
  }

  public partial class NewsFeedsPage
  {
    private void OnImportExportClicked(object? sender, EventArgs e) { }
  }

  public sealed class MediaPlaybackPage : Microsoft.Maui.Controls.ContentPage
  {
    public MediaPlaybackPage(NewsFeedItem item, VouchaApiClient client, ISessionStore sessionStore,
        Voucha.Client.Core.Localization.IUiLocaleController localeController) { }
  }

  internal static class FollowerDistributionActions
  {
    public static bool CanSendRssItem(ISessionStore sessionStore) => false;
    public static Task ShowAsync(Microsoft.Maui.Controls.Page page, IServiceProvider services,
        ISessionStore sessionStore, FollowerDistributionTargetKind kind, string id) => Task.CompletedTask;
  }
}
