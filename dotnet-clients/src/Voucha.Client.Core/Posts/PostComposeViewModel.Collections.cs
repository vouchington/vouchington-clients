namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  private int imageDraftGeneration;

  public void SetReviewTopicRatings(IReadOnlyList<PostComposeTopicRatingDraft> ratings)
  {
    ReviewTopicRatings = ratings;
    OnPropertyChanged(nameof(ReviewTopicRatings));
    OnValidationChanged();
  }

  public void SetRelatedUrls(IReadOnlyList<PostComposeRelatedUrlDraft> urls)
  {
    RelatedUrls = urls;
    OnPropertyChanged(nameof(RelatedUrls));
  }

  public void SetImages(IReadOnlyList<PostComposeImageDraft> images)
  {
    Images = NormalizeImages(images);
    OnPropertyChanged(nameof(Images));
    OnPropertyChanged(nameof(CanAddImages));
    OnPropertyChanged(nameof(IsUploadingImages));
    OnPropertyChanged(nameof(ImageCountLabel));
    OnValidationChanged();
  }

  public void ResetDraft()
  {
    System.Threading.Interlocked.Increment(ref imageDraftGeneration);
    SetPendingLocalPreview(null, false);
    PostType = PostComposeTypes.Discussion;
    Title = "";
    Slug = "";
    Markdown = "";
    CommunitySlug = "";
    LinkAddress = "";
    LinkIdentifier = "";
    Broadcast = "everyone";
    Privacy = "public";
    IsAnonymous = false;
    DeclaredLanguage = "";
    TurnstileToken = "";
    DataPointVertical = "";
    StructuredDataJson = "";
    HpWebsite = "";
    HpPhone = "";
    ReviewTopicId = "";
    ReviewRating = "";
    DiscussionCategoryTopicId = "";
    DiscussionCategoryHashtag = "";
    RelatedLinkIdentifier = "";
    ImageId = "";
    ImageCaption = "";
    SetReviewTopicRatings([]);
    SetDiscussionCategories([]);
    SetRelatedUrls([]);
    SetImages([]);
  }

  private int ImageDraftGeneration => System.Threading.Volatile.Read(ref imageDraftGeneration);

  private bool IsCurrentImageDraftGeneration(int generation) => ImageDraftGeneration == generation;
}
