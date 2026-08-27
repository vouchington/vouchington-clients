using Voucha.Client.App.Support;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class TopicsPage
{
  private async void OnImportExportClicked(object? sender, EventArgs e)
  {
    if (!sessionStore.Current.IsAuthenticated) return;
    await Navigation.PushAsync(new ImportExportPage(
        ImportExportRouteContext.FromPath("/my/topics/import-export"),
        serviceProvider.GetRequiredService<IImportExportService>(),
        serviceProvider.GetRequiredService<IImportExportFileAdapter>(),
        serviceProvider.GetRequiredService<IUiLocalization>(),
        serviceProvider.GetRequiredService<IUiLocaleController>()));
  }
}
