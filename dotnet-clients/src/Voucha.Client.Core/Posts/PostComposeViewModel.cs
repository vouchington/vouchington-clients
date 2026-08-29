using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Relations;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IPostsService postsService;
  private readonly IEntityRelationsService? relationsService;
  private readonly ISessionStore? sessionStore;
  private readonly IImageUploadService? imageUploadService;
  private readonly TimeSpan imageUploadPollInterval;
  private readonly int imageUploadPollAttempts;
  private readonly AppConfig appConfig;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private string postType = PostComposeTypes.Discussion;
  private string title = "";
  private string slug = "";
  private string markdown = "";
  private string communitySlug = "";
  private string linkAddress = "";
  private string linkIdentifier = "";
  private string broadcast = "everyone";
  private string privacy = "public";
  private string declaredLanguage = "";
  private string turnstileToken = "";
  private string dataPointVertical = "";
  private string structuredDataJson = "";
  private string hpWebsite = "";
  private string hpPhone = "";
  private string reviewTopicId = "";
  private string reviewRating = "";
  private string discussionCategoryTopicId = "";
  private string discussionCategoryHashtag = "";
  private string relatedLinkIdentifier = "";
  private string imageId = "";
  private string imageCaption = "";
  private bool isAnonymous;
  private LoadState state = LoadState.Idle;
  private string? errorMessage;

  public EmailVerificationGatedMutation EmailVerificationGate { get; } = new();

  public PostComposeViewModel(
      IPostsService postsService,
      AppConfig appConfig,
      IEntityRelationsService? relationsService = null,
      ISessionStore? sessionStore = null,
      IImageUploadService? imageUploadService = null,
      TimeSpan? imageUploadPollInterval = null,
      int imageUploadPollAttempts = 30,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.postsService = postsService ?? throw new ArgumentNullException(nameof(postsService));
    this.appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
    this.relationsService = relationsService;
    this.sessionStore = sessionStore;
    this.imageUploadService = imageUploadService;
    this.imageUploadPollInterval = imageUploadPollInterval ?? TimeSpan.FromSeconds(2);
    this.imageUploadPollAttempts = Math.Max(1, imageUploadPollAttempts);
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged()
  {
    Images = Images.ToArray();
    OnPropertyChanged(nameof(Images));
  }

  public IReadOnlyList<string> AvailablePostTypes =>
      IsCommunityPost
          ? PostComposeTypes.CommunityTypes
          : IsAdministrator
              ? PostComposeTypes.AdminTypes
              : PostComposeTypes.NonAdminTypes;

  public IReadOnlyList<PostComposeTopicRatingDraft> ReviewTopicRatings { get; private set; } = [];

  public IReadOnlyList<PostComposeCategoryDraft> DiscussionCategories { get; private set; } = [];

  public IReadOnlyList<PostComposeRelatedUrlDraft> RelatedUrls { get; private set; } = [];

  public IReadOnlyList<PostComposeImageDraft> Images { get; private set; } = [];

  public bool CanAddImages => imageUploadService is not null && Images.Count < MaxImages && !IsUploadingImages;

  public bool IsUploadingImages =>
      imageUploadBatchInProgress != 0 ||
      imageUploadInProgress != 0 ||
      Images.Any(image => image.IsUploading);

  public string ImageCountLabel => $"{Images.Count}/{MaxImages}";

  public PostMutationResponse? LastResponse { get; private set; }

  public bool IsPublishing => State == LoadState.Loading;

  public bool HasError => State == LoadState.Error;

  public bool HasPublished => LastResponse is not null;

  public bool IsCommunityPost => !string.IsNullOrWhiteSpace(CommunitySlug);

  public bool IsLinkType => PostType == PostComposeTypes.Link;

  public bool IsReviewType => PostType == PostComposeTypes.Review;

  public bool IsDataPointType => PostType == PostComposeTypes.DataPoint;

  public bool IsDiscussionType => PostType == PostComposeTypes.Discussion;

  public bool CanEditSlug => IsAdministrator;

  public bool CanUseCaptchaBypass => appConfig.AllowPostComposeCaptchaBypass;

  private bool IsAdministrator =>
      sessionStore?.Current.Identity?.Roles?.Contains("administrator", StringComparer.Ordinal) == true;
}
