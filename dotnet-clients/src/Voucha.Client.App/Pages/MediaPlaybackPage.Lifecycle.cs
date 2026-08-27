namespace Voucha.Client.App.Pages;

public sealed partial class MediaPlaybackPage
{
  private bool isDisposed;

  private void RegisterDisposal() => Unloaded += (_, _) => Dispose();

  public void Dispose()
  {
    if (isDisposed) return;
    isDisposed = true;
    localeSubscription.Dispose();
    playbackPositionPersistGate.Dispose();
  }
}
