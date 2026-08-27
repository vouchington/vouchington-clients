using Voucha.Client.App.Pages;
using Voucha.Client.App.Support;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private static bool IsImportExportPath(string path) =>
      path.EndsWith("/import-export", StringComparison.Ordinal);

  private ImportExportPage CreateImportExportPage(string path) => new(
      ImportExportRouteContext.FromPath(path),
      serviceProvider.GetRequiredService<IImportExportService>(),
      serviceProvider.GetRequiredService<IImportExportFileAdapter>(),
      serviceProvider.GetRequiredService<IUiLocalization>(),
      serviceProvider.GetRequiredService<IUiLocaleController>());

  private async Task<bool> OpenOrStoreImportExportRouteAsync(string intentId, string path)
  {
    foreach (var page in EnumerateShellContentPages(intentId))
    {
      await page.Navigation.PushAsync(CreateImportExportPage(path));
      return false;
    }
    return true;
  }
}
