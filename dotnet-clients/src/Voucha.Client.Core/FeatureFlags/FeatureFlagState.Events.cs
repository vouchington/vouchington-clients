namespace Voucha.Client.Core.FeatureFlags;

public sealed partial class FeatureFlagState
{
  private bool Enqueue(FeatureFlagStateChangedEventArgs changed)
  {
    pendingChanges.Enqueue(changed);
    if (changeDrainerActive) return false;
    changeDrainerActive = true;
    return true;
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "All subscribers and queued snapshots must run before publication failures propagate.")]
  private async Task DrainChangesAsync()
  {
    List<Exception>? failures = null;
    while (true)
    {
      FeatureFlagStateChangedEventArgs changed;
      await gate.WaitAsync().ConfigureAwait(false);
      try
      {
        if (!pendingChanges.TryDequeue(out changed!))
        {
          changeDrainerActive = false;
          FinishDrain(failures);
          return;
        }
      }
      finally
      {
        gate.Release();
      }
      foreach (var subscriber in Changed?.GetInvocationList() ?? [])
      {
        try
        {
          ((EventHandler<FeatureFlagStateChangedEventArgs>)subscriber)(this, changed);
        }
        catch (Exception failure)
        {
          (failures ??= []).Add(failure);
        }
      }
    }
  }

  private static void FinishDrain(List<Exception>? failures)
  {
    if (failures is null) return;
    throw failures.Count == 1 ? failures[0] : new AggregateException(failures);
  }
}
