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

  public void SetDiscussionCategoryTopics(IReadOnlyList<PostComposeTopicDraft> topics)
  {
    DiscussionCategoryTopics = topics;
    OnPropertyChanged(nameof(DiscussionCategoryTopics));
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
    RelatedLinkIdentifier = "";
    ImageId = "";
    ImageCaption = "";
    SetReviewTopicRatings([]);
    SetDiscussionCategoryTopics([]);
    SetRelatedUrls([]);
    SetImages([]);
  }

  private int ImageDraftGeneration => System.Threading.Volatile.Read(ref imageDraftGeneration);

  private bool IsCurrentImageDraftGeneration(int generation) => ImageDraftGeneration == generation;
}
