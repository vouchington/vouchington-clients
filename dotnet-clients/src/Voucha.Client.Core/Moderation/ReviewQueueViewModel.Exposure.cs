using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ReviewQueueViewModel
{
  private readonly IModerationExposureService? exposureService;
  private readonly AppConfig appConfig;
  private readonly Func<DateTimeOffset> utcNow;
  private readonly Func<TimeSpan, CancellationToken, Task> delay;
  private readonly HashSet<string> revealedPostIds = new(StringComparer.Ordinal);
  private ModerationExposureState? exposureState;
  private string? revealInFlightPostId;
  private bool isExposureRefreshInFlight;
  private bool isExposureStale = true;
  private long exposureLifecycleVersion;
  private long acceptedExposureLifecycleVersion = -1;
  private long exposureAcceptanceVersion;
  private long revealOutcomeVersion;

  public Task RefreshExposureAsync(CancellationToken cancellationToken = default) =>
      QueueExposureRefreshAsync(scheduleCooldownRefresh: true, cancellationToken);

  public Task RevealMediaAsync(
      ReviewQueueRow row,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    if (exposureService is null || revealInFlightPostId is not null) return Task.CompletedTask;
    var current = Items.FirstOrDefault(item => item.Id == row.Id);
    if (current is not { CanRevealMedia: true })
    {
      return Task.CompletedTask;
    }

    revealInFlightPostId = current.Id;
    RefreshExposureRows();
    return RecordRevealAsync(
        current.Id,
        Volatile.Read(ref revealContextVersion),
        cancellationToken);
  }

  public void CancelExposureOperations()
  {
    Interlocked.Increment(ref exposureLifecycleVersion);
    CancelExposureSchedule();
    if (exposureService is not null) SetExposureStale();
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Any reveal response failure is ambiguous and must keep media gated while gating later reveals.")]
  private async Task RecordRevealAsync(
      string postId,
      long observedRevealContextVersion,
      CancellationToken cancellationToken)
  {
    var observedExposureLifecycleVersion = Volatile.Read(ref exposureLifecycleVersion);
    var observedExposureAcceptanceVersion = Volatile.Read(ref exposureAcceptanceVersion);
    var outcomeVersionAdvanced = false;
    try
    {
      var response = await exposureService!.RecordReviewQueueRevealAsync(
          postId,
          cancellationToken).ConfigureAwait(true);
      Interlocked.Increment(ref revealOutcomeVersion);
      outcomeVersionAdvanced = true;
      if (AcceptsReveal(
          postId,
          observedRevealContextVersion,
          observedExposureLifecycleVersion,
          observedExposureAcceptanceVersion,
          cancellationToken))
      {
        revealedPostIds.Add(postId);
        AcceptExposure(response.Exposure, scheduleCooldownRefresh: true);
      }
      else
      {
        RejectRevealOutcome(observedExposureLifecycleVersion, observedExposureAcceptanceVersion);
      }
    }
    catch (Exception)
    {
      if (!outcomeVersionAdvanced) Interlocked.Increment(ref revealOutcomeVersion);
      RejectRevealOutcome(observedExposureLifecycleVersion, observedExposureAcceptanceVersion);
    }
    finally
    {
      revealInFlightPostId = null;
      RefreshExposureRows();
    }
  }

  private bool AcceptsReveal(
      string postId,
      long observedRevealContextVersion,
      long observedExposureLifecycleVersion,
      long observedExposureAcceptanceVersion,
      CancellationToken cancellationToken) =>
      !cancellationToken.IsCancellationRequested &&
      observedRevealContextVersion == Volatile.Read(ref revealContextVersion) &&
      Items.Any(row => row.Id == postId) &&
      observedExposureLifecycleVersion == Volatile.Read(ref exposureLifecycleVersion) &&
      observedExposureAcceptanceVersion == Volatile.Read(ref exposureAcceptanceVersion);

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Any exposure refresh failure must leave sensitive reveals gated.")]
  private async Task FetchExposureAsync(
      bool scheduleCooldownRefresh,
      CancellationToken cancellationToken)
  {
    if (exposureService is null) return;
    var observedExposureLifecycleVersion = Volatile.Read(ref exposureLifecycleVersion);
    var observedRevealOutcomeVersion = Volatile.Read(ref revealOutcomeVersion);
    isExposureRefreshInFlight = true;
    RefreshExposureRows();
    try
    {
      var response = await exposureService.FetchExposureAsync(cancellationToken)
          .ConfigureAwait(true);
      if (AcceptsExposureRefresh(
          observedExposureLifecycleVersion,
          observedRevealOutcomeVersion,
          cancellationToken))
      {
        AcceptExposure(response.Exposure, scheduleCooldownRefresh);
      }
    }
    catch (Exception)
    {
      if (AcceptsExposureRefresh(
          observedExposureLifecycleVersion,
          observedRevealOutcomeVersion,
          cancellationToken))
      {
        SetExposureStale();
      }
    }
    finally
    {
      isExposureRefreshInFlight = false;
      RefreshExposureRows();
    }
  }

  private bool AcceptsExposureRefresh(
      long observedExposureLifecycleVersion,
      long observedRevealOutcomeVersion,
      CancellationToken cancellationToken) =>
      !cancellationToken.IsCancellationRequested &&
      observedExposureLifecycleVersion == Volatile.Read(ref exposureLifecycleVersion) &&
      observedRevealOutcomeVersion == Volatile.Read(ref revealOutcomeVersion);

  private void AcceptExposure(
      ModerationExposureState next,
      bool scheduleCooldownRefresh)
  {
    exposureState = next;
    Interlocked.Increment(ref exposureAcceptanceVersion);
    Volatile.Write(
        ref acceptedExposureLifecycleVersion,
        Volatile.Read(ref exposureLifecycleVersion));
    isExposureStale = false;
    RefreshExposureRows();
    if (scheduleCooldownRefresh) ScheduleCooldownRefresh(next);
  }

  private void SetExposureStale()
  {
    isExposureStale = true;
    RefreshExposureRows();
  }

  private void RefreshExposureRows() =>
      Items = Items.Select(row => row with
      {
        IsExposureStale = isExposureStale,
        IsInExposureCooldown = exposureState?.InCooldown == true,
        IsMediaRevealed = revealedPostIds.Contains(row.Id),
        IsRevealInFlight = revealInFlightPostId == row.Id,
        IsRevealAvailable = revealInFlightPostId is null && !isExposureRefreshInFlight,
      }).ToArray();
}
