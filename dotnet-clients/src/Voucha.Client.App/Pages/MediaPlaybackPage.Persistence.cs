using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class MediaPlaybackPage
{
  private readonly SemaphoreSlim playbackPositionPersistGate = new(1, 1);
  private int seekDepth;

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "Playback persistence errors should be reported without escaping MAUI event handlers.")]
  private async Task PersistPlaybackPositionAsync(bool force = false, bool completed = false)
  {
    if (!item.IsAudioMedia || !sessionStore.Current.IsAuthenticated)
    {
      return;
    }

    if (force)
    {
      await playbackPositionPersistGate.WaitAsync();
    }
    else if (!await playbackPositionPersistGate.WaitAsync(0))
    {
      return;
    }

    var positionSeconds = Player.Position.TotalSeconds;
    try
    {
      if (!force && Math.Abs(positionSeconds - lastPersistedSeconds) < 10 && DateTimeOffset.UtcNow - lastPersistedAt < TimeSpan.FromSeconds(15))
      {
        return;
      }

      await client.UpdatePodcastPlaybackPositionAsync(item.Id, positionSeconds, completed);
      lastPersistedSeconds = positionSeconds;
      lastPersistedAt = DateTimeOffset.UtcNow;
    }
    catch (Exception ex)
    {
      ReportPlaybackIssue(UiMessageKey.NativeDotnetMediaPlaybackCouldNotSavePlaybackPosition, ex);
    }
    finally
    {
      playbackPositionPersistGate.Release();
    }
  }

  private async void OnOpenExternalAudioClicked(object? sender, EventArgs e)
  {
    if (!item.HasExternalAudioFallback || item.Link is null)
    {
      return;
    }

    await Launcher.Default.OpenAsync(item.Link);
  }

  private static string FormatTime(TimeSpan timeSpan) =>
      timeSpan.TotalSeconds <= 0
          ? UiInvariantText.ZeroDuration.Value
          : $"{(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:00}";

  private async Task ResetCompletedPlaybackAsync()
  {
    hasPlaybackCompleted = false;
    pendingSeekSeconds = 0;
    lastPersistedSeconds = -1;
    try
    {
      EnterSeeking();
      SeekSlider.Value = 0;
      PositionLabel.Text = FormatTime(TimeSpan.Zero);
      await Player.SeekTo(TimeSpan.Zero);
    }
    finally
    {
      ExitSeeking();
    }

    await PersistPlaybackPositionAsync(force: true);
  }

  private void ReportPlaybackIssue(UiMessageKey message, Exception ex)
  {
    System.Diagnostics.Debug.WriteLine(ex);
    SetStatus(message);
  }

  private bool IsSeeking => Volatile.Read(ref seekDepth) > 0;

  private void EnterSeeking() => Interlocked.Increment(ref seekDepth);

  private void ExitSeeking() => Interlocked.Decrement(ref seekDepth);
}
