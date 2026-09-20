namespace Voucha.Client.Core.Moderation;

public sealed partial class ReviewQueueViewModel
{
  private void RejectRevealOutcome(
      long observedExposureLifecycleVersion,
      long observedExposureAcceptanceVersion)
  {
    if (observedExposureAcceptanceVersion != Volatile.Read(ref exposureAcceptanceVersion)) return;
    var currentLifecycleVersion = Volatile.Read(ref exposureLifecycleVersion);
    if (observedExposureLifecycleVersion == currentLifecycleVersion)
    {
      CancelExposureSchedule();
      SetExposureStale();
    }
    else if (Volatile.Read(ref acceptedExposureLifecycleVersion) !=
        currentLifecycleVersion)
    {
      SetExposureStale();
    }
  }
}
