using System.Text.RegularExpressions;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  private const int ReviewMinCharacters = 150;
  private const int ReviewMinWords = 30;
  private const int ReviewMinSentences = 3;
  public PostComposeValidation Validation => Validate(requireCaptcha: true);

  public bool IsValidExceptCaptcha => Validate(requireCaptcha: false).CanPublish;

  private PostComposeValidation Validate(bool requireCaptcha)
  {
    if (IsPublishing) return Invalid(UiMessageKey.NativeDotnetPostsPublishingInProgress);
    if (IsUploadingImages) return Invalid(UiMessageKey.NativeDotnetPostsImageUploadInProgress);
    if (string.IsNullOrWhiteSpace(PostType)) return Invalid(UiMessageKey.NativeDotnetPostsChoosePostType);
    if (PostType is PostComposeTypes.Article or PostComposeTypes.Blog && !IsAdministrator)
    {
      return Invalid(UiMessageKey.NativeDotnetPostsAdministratorsOnlyPostType);
    }
    if (IsCommunityPost && !PostComposeTypes.CommunityTypes.Contains(PostType))
    {
      return Invalid(UiMessageKey.NativeDotnetPostsCommunityPostTypes);
    }
    if (!IsSupportedPostType())
    {
      return Invalid(UiMessageKey.NativeDotnetPostsChooseSupportedPostType);
    }
    if (string.IsNullOrWhiteSpace(Title)) return Invalid(UiMessageKey.NativeDotnetPostsTitleRequired);
    if (string.IsNullOrWhiteSpace(Markdown)) return Invalid(UiMessageKey.NativeDotnetPostsMarkdownRequired);
    if (requireCaptcha && !HasCaptchaToken)
    {
      return new PostComposeValidation(
          false,
          localization.Localize(UiMessageKey.NativeDotnetValidationCaptchaRequired),
          RequiresCaptcha: true);
    }
    if (!HasValidAudience)
    {
      return Invalid(UiMessageKey.NativeDotnetPostsAudienceInvalid);
    }
    if (PostType == PostComposeTypes.Link && !HasValidLinkTarget)
    {
      return Invalid(UiMessageKey.NativeDotnetPostsLinkTargetRequired);
    }
    if (PostType == PostComposeTypes.Review && EffectiveReviewTopicRatings().Count == 0)
    {
      return Invalid(UiMessageKey.NativeDotnetPostsReviewRatingRequired);
    }
    if (PostType == PostComposeTypes.Review && !IsAdministrator && !HasValidReviewContent())
    {
      return Invalid(UiMessageKey.NativeDotnetPostsReviewContentMinimum);
    }
    if (PostType == PostComposeTypes.Review && !HasValidReviewTopicRatings())
    {
      return Invalid(UiMessageKey.NativeDotnetPostsReviewRatingInvalid);
    }
    if (PostType == PostComposeTypes.DataPoint && string.IsNullOrWhiteSpace(DataPointVertical))
    {
      return Invalid(UiMessageKey.NativeDotnetPostsDataPointVerticalRequired);
    }
    if (PostType == PostComposeTypes.DataPoint && string.IsNullOrWhiteSpace(StructuredDataJson))
    {
      return Invalid(UiMessageKey.NativeDotnetPostsStructuredDataRequired);
    }
    if (PostType == PostComposeTypes.DataPoint && !TryParseStructuredData(out _))
    {
      return Invalid(UiMessageKey.NativeDotnetPostsStructuredDataJson);
    }
    if (PostType == PostComposeTypes.DataPoint && !HasValidStructuredData())
    {
      return Invalid(UiMessageKey.NativeDotnetPostsStructuredDataSchema);
    }
    return new PostComposeValidation(true);
  }

  private bool HasCaptchaToken =>
      !string.IsNullOrWhiteSpace(TurnstileToken) || appConfig.AllowPostComposeCaptchaBypass;

  private static readonly HashSet<string> BroadcastValues =
      ["everyone", "users", "followers", "mutual_followers"];

  private static readonly HashSet<string> PrivacyValues = ["public", "private"];

  private bool HasValidAudience =>
      EmptyToNull(Broadcast) is { } broadcast &&
      EmptyToNull(Privacy) is { } privacy &&
      BroadcastValues.Contains(broadcast) &&
      PrivacyValues.Contains(privacy);

  private bool IsSupportedPostType() =>
      IsCommunityPost
          ? PostComposeTypes.CommunityTypes.Contains(PostType)
          : (IsAdministrator ? PostComposeTypes.AdminTypes : PostComposeTypes.NonAdminTypes).Contains(PostType);

  private bool HasValidLinkTarget =>
      EmptyToNull(LinkIdentifier) is { } linkIdentifier
          ? Guid.TryParse(linkIdentifier, out _)
          : Uri.TryCreate(EmptyToNull(LinkAddress), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

  private PostComposeValidation Invalid(UiMessageKey key) =>
      new(false, localization.Localize(key));

  private void OnValidationChanged()
  {
    OnPropertyChanged(nameof(Validation));
    OnPropertyChanged(nameof(IsValidExceptCaptcha));
  }

  private bool HasValidReviewContent() =>
      Markdown.Length >= ReviewMinCharacters &&
      WordRegex().Count(Markdown) >= ReviewMinWords &&
      SentenceRegex().Count(Markdown) >= ReviewMinSentences;

  private bool HasValidReviewTopicRatings()
  {
    var ratings = EffectiveReviewTopicRatings();
    if (ratings.Count is < 1 or > 5) return false;
    var topicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var rating in ratings)
    {
      if (!Guid.TryParse(rating.TopicId, out _) ||
          !topicIds.Add(rating.TopicId) ||
          rating.Rating is < 1 or > 5)
      {
        return false;
      }
    }
    return ratings.Count < 2 || !ratings.All(rating => rating.Rating == ratings[0].Rating);
  }

  [GeneratedRegex(@"\b[\p{L}\p{N}][\p{L}\p{N}'-]*\b")]
  private static partial Regex WordRegex();

  [GeneratedRegex(@"[.!?]+(?:\s|$)")]
  private static partial Regex SentenceRegex();
}
