using System.Globalization;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private CommunityAutomodActionView? settingsAutomodActionView;

  private void UpdateSettingsSwitches()
  {
    settingsAutomodActionView?.Update(viewModel.Community?.AutomodAction);
    if (viewModel.Community is not { } community)
    {
      return;
    }
    if (settingsAllowReviewPostsSwitch is { } review)
    {
      review.IsToggled = community.AllowReviewPosts;
    }
    if (settingsAllowDataPointPostsSwitch is { } dataPoint)
    {
      dataPoint.IsToggled = community.AllowDataPointPosts;
    }
  }

  private IReadOnlyList<CommunitySummaryRow> Rows() =>
      section switch
      {
        CommunityDetailSurfaceSection.Members => viewModel.Members.Select(member => Row(
            member.Id,
            UiText.UserContent(member.DisplayName),
            UiText.ProtocolValue(member.Role),
            Date(member.CreatedAt),
            profileImagePlacement: member.ProfileImagePlacement)).ToArray(),
        CommunityDetailSurfaceSection.Posts => viewModel.Posts.Select(post => Row(
            post.Id,
            UiText.UserContent(post.Title),
            UiText.ProtocolValue(post.PostType),
            Date(post.CreatedAt), post.StaffProvenance,
            sessionStore.Current.Identity?.Roles?.Any(role =>
                role is "administrator" or "moderator") == true)).ToArray(),
        CommunityDetailSurfaceSection.News => viewModel.News,
        CommunityDetailSurfaceSection.Lists => viewModel.ListItemCounts is { } counts
            ? [
                CountRow("topics", UiMessageKey.NativeDotnetCsharpCommunitiesTopics, counts.Topic),
                CountRow("sources", UiMessageKey.NativeDotnetCsharpCommunitiesSources, counts.RssFeed),
                CountRow("posts", UiMessageKey.NativeDotnetCsharpCommunitiesPosts, counts.Post),
                CountRow("domains-urls", UiMessageKey.NativeSwiftRebasedRouteSurfacesDomainsAndUrls, counts.UrlHostname + counts.Url),
            ]
            : [],
        CommunityDetailSurfaceSection.PinnedPosts => viewModel.PinnedPosts,
        CommunityDetailSurfaceSection.Applications => viewModel.Applications,
        CommunityDetailSurfaceSection.Invites => viewModel.Invites,
        CommunityDetailSurfaceSection.Settings => viewModel.Moderation,
        CommunityDetailSurfaceSection.Moderation or CommunityDetailSurfaceSection.Bans or CommunityDetailSurfaceSection.Restrictions or
            CommunityDetailSurfaceSection.ModeratorVacation or CommunityDetailSurfaceSection.AiAgents or CommunityDetailSurfaceSection.AgentPrompts or
            CommunityDetailSurfaceSection.Modlog or CommunityDetailSurfaceSection.Modmail or CommunityDetailSurfaceSection.ModerationAnalytics => viewModel.Moderation,
        _ => [
          CountRow("members", UiMessageKey.NativeDotnetCsharpCommunitiesMembers, viewModel.Members.Count),
          CountRow("posts", UiMessageKey.NativeDotnetCsharpCommunitiesPosts, viewModel.Posts.Count),
        ],
      };

  private static CommunitySummaryRow CountRow(string id, UiMessageKey key, int count) =>
      Row(
          id,
          UiText.Localized(key),
          UiText.Verbatim(UiCopy.FormatNumber(count)));

  private static CommunitySummaryRow Row(
      string id,
      UiText title,
      UiText subtitle,
      UiText? detail = null,
      Voucha.Client.Core.Api.StaffContentProvenance? staffProvenance = null,
      bool isStaffViewer = false,
      TopicImagePlacement? profileImagePlacement = null) =>
      new(id, title, subtitle, detail, UiCopy.CurrentLocalization,
          StaffProvenance: staffProvenance, IsStaffViewer: isStaffViewer,
          ProfileImagePlacement: profileImagePlacement);

  private static UiText? Date(DateTimeOffset? value) =>
      value is { } date ? UiText.Verbatim(UiCopy.FormatDateTime(date)) : null;

  private View RowTemplate()
  {
    var title = new Label { FontAttributes = FontAttributes.Bold };
    title.SetBinding(Label.TextProperty, nameof(CommunitySummaryRow.Title));
    title.SetBinding(Label.FlowDirectionProperty, nameof(CommunitySummaryRow.TitleFlowDirection));
    var authoredContent = new Label();
    authoredContent.SetBinding(Label.TextProperty, nameof(CommunitySummaryRow.AuthoredContent));
    authoredContent.SetBinding(
        Label.FlowDirectionProperty,
        nameof(CommunitySummaryRow.AuthoredContentFlowDirection));
    authoredContent.SetBinding(
        IsVisibleProperty,
        nameof(CommunitySummaryRow.HasAuthoredContent));
    var subtitle = new Label();
    subtitle.SetBinding(Label.TextProperty, nameof(CommunitySummaryRow.Subtitle));
    var detail = new Label();
    detail.SetBinding(Label.TextProperty, nameof(CommunitySummaryRow.Detail));
    var staffChannel = new Label { FontSize = 12, AutomationId = "CommunityPostStaffChannel" };
    staffChannel.SetBinding(Label.TextProperty, nameof(CommunitySummaryRow.StaffChannelLabel));
    staffChannel.SetBinding(IsVisibleProperty, nameof(CommunitySummaryRow.HasStaffChannel));
    var image = new Image { HeightRequest = 160, Aspect = Aspect.AspectFit };
    image.SetBinding(Image.SourceProperty, "EmbedPreview.ThumbnailUrl");
    var provider = new Label { FontSize = 12 };
    provider.SetBinding(Label.TextProperty, "EmbedPreview.Provider");
    var embedTitle = new Label { FontAttributes = FontAttributes.Bold };
    embedTitle.SetBinding(Label.TextProperty, "EmbedPreview.Title");
    var description = new Label { FontSize = 12 };
    description.SetBinding(Label.TextProperty, "EmbedPreview.Description");
    var play = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetMediaPlaybackPlay);
    play.SetBinding(IsVisibleProperty, "EmbedPreview.CanPlay");
    play.Clicked += OnEmbedPlayClicked;
    var open = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeSwiftPodcastPlaybackOpenSource);
    open.SetBinding(IsVisibleProperty, "EmbedPreview.HasSource");
    open.Clicked += OnEmbedOpenClicked;
    var embed = new VerticalStackLayout { Spacing = 3, Children = { image, provider, embedTitle, description, play, open } };
    embed.SetBinding(IsVisibleProperty, "EmbedPreview.HasPreview");
    var text = new VerticalStackLayout
    {
      Spacing = 3,
      Children = { title, authoredContent, subtitle, detail, staffChannel, embed },
    };
    if (section != CommunityDetailSurfaceSection.Members)
    {
      return text;
    }

    title.LineBreakMode = LineBreakMode.WordWrap;
    var avatarImage = new Image
    {
      Aspect = Aspect.AspectFill,
      AutomationId = "community-member-avatar-image",
    };
    avatarImage.SetBinding(
        Image.SourceProperty,
        new Binding(nameof(CommunitySummaryRow.ProfileImagePlacement), converter: new PlacementImageSourceConverter(config, 96)));
    AutomationProperties.SetIsInAccessibleTree(avatarImage, false);
    var avatar = new Border
    {
      WidthRequest = 40,
      HeightRequest = 40,
      StrokeThickness = 0,
      StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
      Content = avatarImage,
      AutomationId = "community-member-avatar",
    };
    AutomationProperties.SetIsInAccessibleTree(avatar, false);
    avatar.SetBinding(
        IsVisibleProperty,
        new Binding(nameof(CommunitySummaryRow.ProfileImagePlacement), converter: new HasProfileImagePlacementConverter(config, 96)));
    var avatarColumn = new ColumnDefinition(new GridLength(40));
    var row = new Grid
    {
      ColumnDefinitions =
      {
        avatarColumn,
        new ColumnDefinition(GridLength.Star),
      },
      ColumnSpacing = 8,
      Children = { avatar, text },
    };
    void UpdateAvatarLayout(object? sender, EventArgs args)
    {
      var hasImage = row.BindingContext is CommunitySummaryRow { ProfileImagePlacement: { } placement } &&
          HasImageUrl(placement, config, 96);
      avatarColumn.Width = new GridLength(hasImage ? 40 : 0);
      row.ColumnSpacing = hasImage ? 8 : 0;
    }
    row.BindingContextChanged += UpdateAvatarLayout;
    UpdateAvatarLayout(row, EventArgs.Empty);
    Grid.SetColumn(text, 1);
    return row;
  }

  private sealed class HasProfileImagePlacementConverter(AppConfig config, int width) : IValueConverter
  {
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        HasImageUrl(value, config, width);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
  }

  private static bool HasImageUrl(object? value, AppConfig config, int width) =>
      value is TopicImagePlacement { } placement &&
      config.ImageUrlForPlacement(placement.PlacementId, placement.PlacementRevision, placement.ImageId, width) is not null;
}
