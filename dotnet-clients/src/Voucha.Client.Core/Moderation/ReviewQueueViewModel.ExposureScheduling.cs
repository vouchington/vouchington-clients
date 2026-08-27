using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ReviewQueueViewModel
{
  private CancellationTokenSource? cooldownRefreshCancellation;

  private void ScheduleCooldownRefresh(ModerationExposureState next)
  {
    CancelExposureSchedule();
    if (!next.InCooldown || next.CooldownEndsAt is not { } cooldownEndsAt) return;

    var cancellation = new CancellationTokenSource();
    cooldownRefreshCancellation = cancellation;
    _ = RefreshAfterCooldownAsync(
        Max(TimeSpan.Zero, cooldownEndsAt - utcNow()),
        cancellation);
  }

  private async Task RefreshAfterCooldownAsync(
      TimeSpan wait,
      CancellationTokenSource cancellation)
  {
    try
    {
      await delay(wait, cancellation.Token).ConfigureAwait(true);
      if (cancellation.IsCancellationRequested) return;
      SetExposureStale();
      await QueueExposureRefreshAsync(
          scheduleCooldownRefresh: false,
          cancellation.Token).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
    finally
    {
      if (ReferenceEquals(cooldownRefreshCancellation, cancellation))
      {
        cooldownRefreshCancellation = null;
      }
      cancellation.Dispose();
    }
  }

  private void CancelExposureSchedule()
  {
    var cancellation = cooldownRefreshCancellation;
    cooldownRefreshCancellation = null;
    cancellation?.Cancel();
  }

  private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
      left >= right ? left : right;
}
