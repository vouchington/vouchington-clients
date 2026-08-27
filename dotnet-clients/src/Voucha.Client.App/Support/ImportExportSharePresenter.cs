using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Support;

public interface IImportExportSharePresenter
{
  Task ShareAsync(string path, string mediaType, CancellationToken token);
}

public sealed class MauiImportExportSharePresenter : IImportExportSharePresenter
{
  public async Task ShareAsync(string path, string mediaType, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();
    await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest
    {
      Title = UiCopy.Localize(UiMessageKey.NativeSwiftImportExportShareExport),
      File = new ShareFile(path, mediaType),
    })).ConfigureAwait(false);
  }
}
