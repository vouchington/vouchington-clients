namespace Voucha.Client.Core.Moderation;

public sealed partial class ReviewQueueViewModel
{
  private readonly object exposureRefreshSync = new();
  private Task exposureRefreshSettlement = Task.CompletedTask;

  private Task QueueExposureRefreshAsync(
      bool scheduleCooldownRefresh,
      CancellationToken cancellationToken)
  {
    if (exposureService is null) return Task.CompletedTask;
    var requestedLifecycleVersion = Volatile.Read(ref exposureLifecycleVersion);
    lock (exposureRefreshSync)
    {
      var retryOnlyIfStale = !exposureRefreshSettlement.IsCompleted;
      exposureRefreshSettlement = RunQueuedExposureRefreshAsync(
          exposureRefreshSettlement,
          requestedLifecycleVersion,
          retryOnlyIfStale,
          scheduleCooldownRefresh,
          cancellationToken);
      return exposureRefreshSettlement;
    }
  }

  private async Task RunQueuedExposureRefreshAsync(
      Task precedingRefresh,
      long requestedLifecycleVersion,
      bool retryOnlyIfStale,
      bool scheduleCooldownRefresh,
      CancellationToken cancellationToken)
  {
    await precedingRefresh.ConfigureAwait(true);
    if (cancellationToken.IsCancellationRequested ||
        requestedLifecycleVersion != Volatile.Read(ref exposureLifecycleVersion) ||
        retryOnlyIfStale && !isExposureStale)
    {
      return;
    }
    await FetchExposureAsync(scheduleCooldownRefresh, cancellationToken).ConfigureAwait(true);
  }
}
