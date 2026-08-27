using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class MediaPlaybackPage
{
  [System.Diagnostics.CodeAnalysis.SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void lifecycle methods must not allow playback failures to escape to the dispatcher.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    isActive = true;
    isLoaded = false;
    pendingSeekSeconds = 0;

    if (!item.HasDirectPlayback)
    {
      return;
    }

    Player.MetadataTitle = item.Title;
    Player.MetadataArtist = item.Source;
    Player.MetadataArtworkUrl = item.ThumbnailUrl?.ToString() ?? string.Empty;
    Player.Speed = 1.0;

    if (item.IsAudioMedia && item.HasDirectPlayback)
    {
      _ = LoadPodcastChaptersAsync();
    }

    if (item.IsAudioMedia && sessionStore.Current.IsAuthenticated)
    {
      try
      {
        var response = await client.FetchPodcastPlaybackPositionAsync(item.Id);
        if (!isActive)
        {
          return;
        }

        if (response.PlaybackPosition is { } playbackPosition)
        {
          pendingSeekSeconds = playbackPosition.IsCompleted ? 0 : playbackPosition.PositionSeconds;
        }
      }
      catch (Exception ex)
      {
        if (isActive)
        {
          ReportPlaybackIssue(
              UiMessageKey.NativeDotnetMediaPlaybackCouldNotRestorePlaybackPosition,
              ex);
        }
      }
    }

    if (!isActive)
    {
      return;
    }

    try
    {
      Player.Source = item.MediaUrl;
    }
    catch (Exception ex)
    {
      if (isActive)
      {
        ReportPlaybackIssue(UiMessageKey.NativeDotnetMediaPlaybackCouldNotLoadPlayback, ex);
      }
    }
  }

  protected override async void OnDisappearing()
  {
    isActive = false;

    if (item.HasDirectPlayback)
    {
      Player.Pause();
      isPlaying = false;
    }

    if (hasPlaybackStarted && !hasPlaybackCompleted)
    {
      await PersistPlaybackPositionAsync(force: true);
    }

    if (item.HasDirectPlayback)
    {
      Player.Source = null;
    }

    podcastChapters = [];
    ChapterButtonsLayout.Children.Clear();
    ChapterPanel.IsVisible = false;
    isLoaded = false;
    hasPlaybackStarted = false;
    hasPlaybackCompleted = false;
    pendingSeekSeconds = 0;
    base.OnDisappearing();
  }

  private async void OnMediaOpened(object? sender, EventArgs e)
  {
    if (!item.HasDirectPlayback || !isActive || isLoaded)
    {
      return;
    }

    isLoaded = true;
    DurationLabel.Text = FormatTime(Player.Duration);
    SeekSlider.Maximum = Math.Max(1, Player.Duration.TotalSeconds);
    UpdatePodcastChapterButtonStates();
    if (pendingSeekSeconds > 0)
    {
      try
      {
        EnterSeeking();
        SeekSlider.Value = pendingSeekSeconds;
        await Player.SeekTo(TimeSpan.FromSeconds(pendingSeekSeconds));
      }
      finally
      {
        ExitSeeking();
      }
    }

    if (!isActive)
    {
      return;
    }

    Player.Play();
    isPlaying = true;
    hasPlaybackStarted = true;
  }
}
