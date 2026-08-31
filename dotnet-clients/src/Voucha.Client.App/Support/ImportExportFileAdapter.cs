using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Support;

public sealed class MauiImportExportFilePicker : IImportExportFilePicker
{
  public async Task<PickedImportFile?> PickAsync(CancellationToken token)
  {
    token.ThrowIfCancellationRequested();
    var file = await FilePicker.Default.PickAsync(new PickOptions
    {
      PickerTitle = UiCopy.Localize(UiMessageKey.NativeSwiftImportExportChooseFile),
      FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
      {
        [DevicePlatform.WinUI] = [".csv", ".opml", ".xml"],
        [DevicePlatform.MacCatalyst] = ["public.comma-separated-values-text", "public.xml", "ai.voucha.opml"],
      }),
    }).ConfigureAwait(true);
    return file is null ? null : new(file.FileName, _ => file.OpenReadAsync());
  }
}

public sealed class MauiImportExportDocumentSharer(
    IImportExportSharePresenter presenter) : IImportExportDocumentSharer
{
  public async Task ShareAsync(ExportDocument document, CancellationToken token)
  {
    await ExportShareFileStager.StageAndUseAsync(
        FileSystem.CacheDirectory,
        document,
        (path, shareToken) => presenter.ShareAsync(path, document.MediaType, shareToken),
        token).ConfigureAwait(false);
  }
}
