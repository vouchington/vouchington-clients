using Voucha.Client.App.Support;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.App.Pages;

public partial class NewsFeedsPage
{
  private async void OnImportExportClicked(object? sender, EventArgs e)
  {
    if (!sessionStore.Current.IsAuthenticated) return;
    var path = viewModel.FeedKind switch
    {
      NewsFeedKind.Podcasts => "/my/podcasts/import-export",
      NewsFeedKind.Videos => "/my/channels/import-export",
      _ => "/my/news-sources/import-export",
    };
    await Navigation.PushAsync(new ImportExportPage(
        ImportExportRouteContext.FromPath(path),
        serviceProvider.GetRequiredService<IImportExportService>(),
        serviceProvider.GetRequiredService<IImportExportFileAdapter>(),
        serviceProvider.GetRequiredService<IUiLocalization>(),
        serviceProvider.GetRequiredService<IUiLocaleController>()));
  }
}
