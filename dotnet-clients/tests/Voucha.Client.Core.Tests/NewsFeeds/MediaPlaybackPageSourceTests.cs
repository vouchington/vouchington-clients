using Xunit;

namespace Voucha.Client.Core.Tests.NewsFeeds;

public sealed class MediaPlaybackPageSourceTests
{
  [Fact]
  public void DirectMediaPageWiresPlaybackAndSeekingControls()
  {
    var xaml = ReadPage("MediaPlaybackPage.xaml");
    var page = ReadPage("MediaPlaybackPage.xaml.cs");
    var playback = ReadPage("MediaPlaybackPage.Playback.cs");

    Assert.Contains("x:Name=\"Player\"", xaml, StringComparison.Ordinal);
    Assert.Contains("Clicked=\"OnPlayClicked\"", xaml, StringComparison.Ordinal);
    Assert.Contains("Clicked=\"OnPauseClicked\"", xaml, StringComparison.Ordinal);
    Assert.Contains("ValueChanged=\"OnSeekValueChanged\"", xaml, StringComparison.Ordinal);
    Assert.Contains("PlayerPanel.IsVisible = item.HasDirectPlayback;", page, StringComparison.Ordinal);
    Assert.Contains("Player.Source = item.MediaUrl;", playback, StringComparison.Ordinal);
    Assert.Contains("Player.Play();", playback, StringComparison.Ordinal);
    Assert.Contains("Player.SeekTo(TimeSpan.FromSeconds(pendingSeekSeconds))", playback, StringComparison.Ordinal);
  }

  [Fact]
  public void DirectMediaPageWiresResumeChaptersSpeedAndLifecycleCleanup()
  {
    var xaml = ReadPage("MediaPlaybackPage.xaml");
    var playback = ReadPage("MediaPlaybackPage.Playback.cs");
    var persistence = ReadPage("MediaPlaybackPage.Persistence.cs");
    var chapters = ReadPage("MediaPlaybackPage.Chapters.cs");
    var speed = ReadPage("MediaPlaybackPage.Speed.cs");

    Assert.Contains("x:Name=\"ChapterButtonsLayout\"", xaml, StringComparison.Ordinal);
    Assert.Contains("FetchPodcastPlaybackPositionAsync(item.Id)", playback, StringComparison.Ordinal);
    Assert.Contains("UpdatePodcastPlaybackPositionAsync(item.Id, positionSeconds, completed)", persistence, StringComparison.Ordinal);
    Assert.Contains("FetchPodcastEpisodeChaptersAsync(item.Id)", chapters, StringComparison.Ordinal);
    Assert.Contains("await ApplyUserSeekAsync(chapter.StartSeconds);", chapters, StringComparison.Ordinal);
    Assert.Contains("Player.Speed = speed;", speed, StringComparison.Ordinal);
    Assert.Contains("Player.Source = null;", playback, StringComparison.Ordinal);
    Assert.Contains("await PersistPlaybackPositionAsync(force: true);", playback, StringComparison.Ordinal);
  }

  [Fact]
  public void EmbedOnlyVideoIsUnavailableAcrossListDetailAndPlaybackPage()
  {
    var list = ReadPage("NewsFeedsPage.xaml");
    var detail = ReadPage("RssFeedItemDetailPage.cs");
    var playbackXaml = ReadPage("MediaPlaybackPage.xaml");
    var playbackPage = ReadPage("MediaPlaybackPage.xaml.cs");

    Assert.Contains("IsVisible=\"{Binding IsEmbedOnlyVideo}\"", list, StringComparison.Ordinal);
    Assert.Contains("NativeDotnetMediaPlaybackVideoUnavailable", detail, StringComparison.Ordinal);
    Assert.Contains("VerticalTextAlignment = TextAlignment.Center", detail, StringComparison.Ordinal);
    Assert.Contains("FontSize = 13", detail, StringComparison.Ordinal);
    Assert.Contains("x:Name=\"VideoUnavailablePanel\"", playbackXaml, StringComparison.Ordinal);
    Assert.Contains("native.dotnet.mediaPlayback.videoUnavailable", playbackXaml, StringComparison.Ordinal);
    Assert.Contains("VideoUnavailablePanel.IsVisible = item.IsEmbedOnlyVideo;", playbackPage, StringComparison.Ordinal);
  }

  [Fact]
  public void ListAndDetailOnlyNavigateWhenMediaPlaybackIsAvailable()
  {
    var list = ReadPage("NewsFeedsPage.xaml.cs");
    var detail = ReadPage("RssFeedItemDetailPage.cs");
    var listXaml = ReadPage("NewsFeedsPage.xaml");

    Assert.Contains("!item.HasMediaPlayback", list, StringComparison.Ordinal);
    Assert.Contains("HasMediaPlayback: true", detail, StringComparison.Ordinal);
    Assert.Contains("native.swift.podcastPlayback.openSource", listXaml, StringComparison.Ordinal);
    Assert.DoesNotContain("native.swift.communityActions.open", listXaml, StringComparison.Ordinal);
  }

  [Fact]
  public void ExternalLauncherIsRestrictedToAudioFallback()
  {
    var xaml = ReadPage("MediaPlaybackPage.xaml");
    var persistence = ReadPage("MediaPlaybackPage.Persistence.cs");

    Assert.Contains("x:Name=\"ExternalAudioFallbackPanel\"", xaml, StringComparison.Ordinal);
    Assert.Contains("Clicked=\"OnOpenExternalAudioClicked\"", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("OnOpenExternalClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("if (!item.HasExternalAudioFallback || item.Link is null)", persistence, StringComparison.Ordinal);
    Assert.Contains("await Launcher.Default.OpenAsync(item.Link);", persistence, StringComparison.Ordinal);
  }

  private static string ReadPage(string file) => File.ReadAllText(RepoPath(
      "dotnet-clients",
      "src",
      "Voucha.Client.App",
      "Pages",
      file));

  private static string RepoPath(params string[] parts)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
      var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
      if (File.Exists(candidate))
      {
        return candidate;
      }

      directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}
