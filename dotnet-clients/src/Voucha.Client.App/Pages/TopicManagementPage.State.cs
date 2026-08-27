using Voucha.Client.Core.Api;

namespace Voucha.Client.App.Pages;

public partial class TopicManagementPage
{
  private void ApplyTopic(Topic topic)
  {
    NameEntry.Text = topic.Name;
    SlugEntry.Text = topic.Slug;
    TypeEntry.Text = topic.TopicType;
    MarkdownEditor.Markdown = topic.Markdown ?? string.Empty;
    HostnameEntry.Text = topic.Hostname?.Hostname;
    LogoImageEntry.Text = topic.LogoImageId;
    HeroImageEntry.Text = topic.HeroImageId;
    NoindexCheckBox.IsChecked = topic.Noindex == true;
    AllowReviewsCheckBox.IsChecked = topic.AllowReviews != false;
  }
}
