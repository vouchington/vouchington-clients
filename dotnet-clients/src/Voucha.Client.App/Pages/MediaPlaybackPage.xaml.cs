using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.NewsFeeds;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class MediaPlaybackPage :
    ContentPage,
    IUiLocaleChangeListener,
    IDisposable
{
  private readonly NewsFeedItem item;
  private readonly VouchaApiClient client;
  private readonly ISessionStore sessionStore;
  private bool isPlaying;
  private bool isLoaded;
  private bool isActive;
  private bool hasPlaybackStarted;
  private bool hasPlaybackCompleted;
  private bool isUserSeekInFlight;
  private double? pendingUserSeekSeconds;
  private double pendingSeekSeconds;
  private DateTimeOffset lastPersistedAt = DateTimeOffset.MinValue;
  private double lastPersistedSeconds = -1;
  private UiMessageKey? statusMessageKey;
  private readonly IDisposable localeSubscription;

  public MediaPlaybackPage(
      NewsFeedItem item,
      VouchaApiClient client,
      ISessionStore sessionStore,
      IUiLocaleController localeController)
  {
    InitializeComponent();
    this.item = item;
    this.client = client;
    this.sessionStore = sessionStore;
    localeSubscription = (localeController ?? throw new ArgumentNullException(nameof(localeController)))
        .SubscribeLocaleChanges(this);
    RegisterDisposal();
    Title = item.Title;
    TitleLabel.Text = item.Title;
    SourceLabel.Text = item.Source;
    SummaryLabel.Text = item.Summary;
    LinkLabel.Text = UiExternalContentText.FromUri(item.Link).Value;
    ExternalAudioFallbackPanel.IsVisible = item.HasExternalAudioFallback;
    VideoUnavailablePanel.IsVisible = item.IsEmbedOnlyVideoUnavailable;
    PlayerPanel.IsVisible = item.HasDirectPlayback;
    ChapterPanel.IsVisible = false;
    SeekSlider.IsEnabled = item.HasDirectPlayback;
    hasPlaybackStarted = false;
    hasPlaybackCompleted = false;
    PositionLabel.Text = UiInvariantText.ZeroDuration.Value;
    DurationLabel.Text = UiInvariantText.UnknownDuration.Value;
    StatusLabel.Text = UiCopy.Resolve(MediaMetadataText(item));
  }

  private async void OnPlayClicked(object? sender, EventArgs e)
  {
    if (!item.HasDirectPlayback || !isLoaded)
    {
      return;
    }

    if (hasPlaybackCompleted)
    {
      await ResetCompletedPlaybackAsync();
    }

    Player.Play();
    isPlaying = true;
    hasPlaybackStarted = true;
  }

  private async void OnPauseClicked(object? sender, EventArgs e)
  {
    if (!item.HasDirectPlayback || !isLoaded || !hasPlaybackStarted)
    {
      return;
    }

    Player.Pause();
    isPlaying = false;
    await PersistPlaybackPositionAsync(force: true);
  }

  private async void OnMediaEnded(object? sender, EventArgs e)
  {
    isPlaying = false;
    hasPlaybackCompleted = true;
    await PersistPlaybackPositionAsync(force: true, completed: true);
  }

  private void OnMediaFailed(object? sender, EventArgs e)
  {
    SetStatus(UiMessageKey.NativeDotnetMediaPlaybackPlaybackStateFailed);
  }

  private async void OnPositionChanged(object? sender, EventArgs e)
  {
    if (!item.HasDirectPlayback)
    {
      return;
    }

    try
    {
      EnterSeeking();
      SeekSlider.Value = Player.Position.TotalSeconds;
      PositionLabel.Text = FormatTime(Player.Position);
      DurationLabel.Text = FormatTime(Player.Duration);
    }
    finally
    {
      ExitSeeking();
    }

    if (item.IsAudioMedia && isPlaying)
    {
      await PersistPlaybackPositionAsync();
    }
  }

  private void OnStateChanged(object? sender, EventArgs e)
  {
    SetStatus(Player.CurrentState.ToString() switch
    {
      "Buffering" => UiMessageKey.NativeDotnetMediaPlaybackPlaybackStateBuffering,
      "Failed" => UiMessageKey.NativeDotnetMediaPlaybackPlaybackStateFailed,
      "Opening" => UiMessageKey.NativeDotnetMediaPlaybackPlaybackStateOpening,
      "Paused" => UiMessageKey.NativeDotnetMediaPlaybackPlaybackStatePaused,
      "Playing" => UiMessageKey.NativeDotnetMediaPlaybackPlaybackStatePlaying,
      "Stopped" => UiMessageKey.NativeDotnetMediaPlaybackPlaybackStateStopped,
      _ => UiMessageKey.NativeDotnetMediaPlaybackPlaybackStateNone,
    });
  }

  private async void OnSeekValueChanged(object? sender, ValueChangedEventArgs e)
  {
    if (!item.HasDirectPlayback || !isLoaded || !hasPlaybackStarted || IsSeeking)
    {
      return;
    }

    await ApplyUserSeekAsync(e.NewValue);
  }

  private async Task ApplyUserSeekAsync(double seekSeconds)
  {
    if (isUserSeekInFlight)
    {
      pendingUserSeekSeconds = seekSeconds;
      return;
    }

    isUserSeekInFlight = true;
    try
    {
      var targetSeconds = seekSeconds;
      while (true)
      {
        pendingUserSeekSeconds = null;
        await Player.SeekTo(TimeSpan.FromSeconds(targetSeconds));
        PositionLabel.Text = FormatTime(TimeSpan.FromSeconds(targetSeconds));
        await PersistPlaybackPositionAsync(force: true);
        if (pendingUserSeekSeconds is not { } nextSeconds || Math.Abs(nextSeconds - targetSeconds) < 0.001)
        {
          return;
        }

        targetSeconds = nextSeconds;
      }
    }
    finally
    {
      isUserSeekInFlight = false;
    }
  }

  private void SetStatus(UiMessageKey key)
  {
    statusMessageKey = key;
    StatusLabel.Text = UiCopy.Localize(key);
  }

  public void OnUiLocaleChanged()
  {
    if (statusMessageKey is { } key) StatusLabel.Text = UiCopy.Localize(key);
    RenderPodcastChapters();
  }

}
