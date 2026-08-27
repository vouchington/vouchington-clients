namespace Voucha.Client.App.Pages;

public sealed partial class MemberAppealsPage
{
  private void CancelOperations()
  {
    submissionCancellation?.Cancel();
    loadCancellation?.Cancel();
    lifetimeCancellation.Cancel();
    lifetimeCancellation.Dispose();
    isLoaded = false;
    initialLoad = null;
  }

  private void EnsureLifetime()
  {
    if (!lifetimeCancellation.IsCancellationRequested) return;
    lifetimeCancellation = new CancellationTokenSource();
  }
}
