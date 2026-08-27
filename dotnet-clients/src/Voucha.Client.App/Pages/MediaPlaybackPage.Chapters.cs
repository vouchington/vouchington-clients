using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class MediaPlaybackPage
{
  private IReadOnlyList<PodcastEpisodeChapter> podcastChapters = [];

  private async Task LoadPodcastChaptersAsync()
  {
    if (!item.IsAudioMedia || !item.HasDirectPlayback)
    {
      return;
    }

    try
    {
      var response = await client.FetchPodcastEpisodeChaptersAsync(item.Id);
      if (!isActive)
      {
        return;
      }

      podcastChapters = response.Chapters.Where(chapter => chapter.IsVisible).ToArray();
      RenderPodcastChapters();
    }
    catch (Exception ex)
    {
      if (isActive)
      {
        ReportPlaybackIssue(UiMessageKey.NativeDotnetMediaPlaybackCouldNotLoadChapters, ex);
      }
    }
  }

  private void RenderPodcastChapters()
  {
    ChapterButtonsLayout.Children.Clear();
    ChapterPanel.IsVisible = podcastChapters.Count > 0;

    foreach (var chapter in podcastChapters)
    {
      var chapterButton = new Button
      {
        Text = FormatPodcastChapterLabel(chapter),
        HorizontalOptions = LayoutOptions.Fill,
      };

      SemanticProperties.SetDescription(
          chapterButton,
          UiCopy.Format(
              UiMessageKey.NativeDotnetMediaPlaybackJumpTo,
              ("target", FormatPodcastChapterLabel(chapter))));
      chapterButton.Clicked += (_, _) => OnPodcastChapterClicked(chapter);
      ChapterButtonsLayout.Children.Add(chapterButton);
    }

    UpdatePodcastChapterButtonStates();
  }

  private async void OnPodcastChapterClicked(PodcastEpisodeChapter chapter)
  {
    if (!item.HasDirectPlayback || !isActive)
    {
      return;
    }

    if (!isLoaded)
    {
      return;
    }

    await ApplyUserSeekAsync(chapter.StartSeconds);
  }

  private static string FormatPodcastChapterLabel(PodcastEpisodeChapter chapter)
  {
    var time = FormatTime(TimeSpan.FromSeconds(chapter.StartSeconds));
    return string.IsNullOrWhiteSpace(chapter.Title) ? time : $"{time} {chapter.Title}";
  }

  private void UpdatePodcastChapterButtonStates()
  {
    foreach (var child in ChapterButtonsLayout.Children.OfType<Button>())
    {
      child.IsEnabled = isLoaded;
    }
  }
}
