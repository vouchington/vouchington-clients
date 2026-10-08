using Voucha.Client.Core.Api;

namespace Voucha.Client.App.Pages;

public partial class TopicManagementPage
{
  private void ApplyTopic(Topic topic)
  {
    logoPreviewGeneration++;
    heroPreviewGeneration++;
    NameEntry.Text = topic.Name;
    SlugEntry.Text = topic.Slug;
    TypeEntry.Text = topic.TopicType;
    MarkdownEditor.Markdown = topic.Markdown ?? string.Empty;
    HostnameEntry.Text = topic.Hostname?.Hostname;
    LogoImageEntry.Text = topic.LogoImageId;
    HeroImageEntry.Text = topic.HeroImageId;
    logoPlacement = topic.LogoImagePlacement;
    heroPlacement = topic.HeroImagePlacement;
    ClearLocalPreview(LogoLocalPreviewImage);
    ClearLocalPreview(HeroLocalPreviewImage);
    LogoPreviewUnavailableLabel.IsVisible = false;
    HeroPreviewUnavailableLabel.IsVisible = false;
    logoLocalImageId = null;
    heroLocalImageId = null;
    UpdatePersistedPreviews();
    NoindexCheckBox.IsChecked = topic.Noindex == true;
    AllowReviewsCheckBox.IsChecked = topic.AllowReviews != false;
  }

  private void OnImageIdChanged(object? sender, TextChangedEventArgs e)
  {
    if (ReferenceEquals(sender, LogoImageEntry) &&
        !string.Equals(e.NewTextValue, logoLocalImageId, StringComparison.Ordinal))
    {
      logoPreviewGeneration++;
      ClearLocalPreview(LogoLocalPreviewImage);
      LogoPreviewUnavailableLabel.IsVisible = false;
      logoLocalImageId = null;
    }
    if (ReferenceEquals(sender, HeroImageEntry) &&
        !string.Equals(e.NewTextValue, heroLocalImageId, StringComparison.Ordinal))
    {
      heroPreviewGeneration++;
      ClearLocalPreview(HeroLocalPreviewImage);
      HeroPreviewUnavailableLabel.IsVisible = false;
      heroLocalImageId = null;
    }
    UpdatePersistedPreviews();
  }

  private void UpdatePersistedPreviews()
  {
    SetPersistedPreview(LogoPersistedImage, LogoImageEntry.Text, logoPlacement);
    SetPersistedPreview(HeroPersistedImage, HeroImageEntry.Text, heroPlacement);
  }

  private void SetPersistedPreview(Image image, string? enteredId, TopicImagePlacement? placement)
  {
    var url = placement is not null && string.Equals(enteredId, placement.ImageId, StringComparison.Ordinal)
        ? config.ImageUrlForPlacement(placement.PlacementId, placement.PlacementRevision, placement.ImageId, 480)
        : null;
    image.Source = url is null ? null : ImageSource.FromUri(url);
    image.IsVisible = url is not null;
  }

  private static void ClearLocalPreview(Image image)
  {
    image.Source = null;
    image.IsVisible = false;
  }
}
