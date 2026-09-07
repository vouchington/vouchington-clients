using Voucha.Client.Core.Communities;

namespace Voucha.Client.App.Pages;

public sealed partial class CommunityDetailPage
{
  private void RenderPostRows(VerticalStackLayout layout, IEnumerable<CommunityPostRow> rows)
  {
    layout.Children.Clear();
    foreach (var row in rows)
    {
      var content = new VerticalStackLayout { Spacing = 4 };
      var title = new Label { Text = row.Title };
      if (row.TitleFlowDirection is { } direction)
        title.FlowDirection = direction == "RightToLeft" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
      content.Add(title);
      content.Add(new Label { Text = row.PostType, FontSize = 12 });
      if (row.EmbedPreview is { } preview)
      {
        if (preview.ThumbnailUrl is { } thumbnail) content.Add(new Image { HeightRequest = 160, Aspect = Aspect.AspectFit, Source = thumbnail });
        if (preview.Provider is { } provider) content.Add(new Label { Text = provider, FontSize = 12 });
        if (preview.Title is { } title) content.Add(new Label { Text = title, FontAttributes = FontAttributes.Bold });
        if (preview.Description is { } description) content.Add(new Label { Text = description, FontSize = 12 });
        if (preview.CanPlay && preview.PlayerUrl is { } playerUrl && preview.SourceUrl is { } sourceUrl)
        {
          var play = new Button { Text = UiCopy.Localize(UiMessageKey.NativeDotnetMediaPlaybackPlay) };
          play.Clicked += async (_, _) => await Navigation.PushAsync(new EmbedPlayerPage(playerUrl, sourceUrl)).ConfigureAwait(true);
          content.Add(play);
        }
        if (preview.SourceUrl is { } sourceUrl)
        {
          var open = new Button { Text = UiCopy.Localize(UiMessageKey.NativeSwiftPodcastPlaybackOpenSource) };
          open.Clicked += async (_, _) => await Launcher.Default.OpenAsync(sourceUrl).ConfigureAwait(true);
          content.Add(open);
        }
      }
      layout.Add(content);
    }
  }
}
