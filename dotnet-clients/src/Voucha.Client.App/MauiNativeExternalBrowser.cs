using Voucha.Client.Core.Fediverse;

namespace Voucha.Client.App;

public sealed class MauiNativeExternalBrowser : INativeExternalBrowser
{
  public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
      Launcher.Default.OpenAsync(uri);
}
