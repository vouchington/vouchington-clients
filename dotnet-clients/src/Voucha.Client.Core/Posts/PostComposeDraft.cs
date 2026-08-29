using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Posts;

public static class PostComposeTypes
{
  public const string Discussion = "discussion";
  public const string Review = "review";
  public const string DataPoint = "data_point";
  public const string Link = "link";
  public const string Article = "article";
  public const string Blog = "blog_post";

  public static readonly string[] CommunityTypes = [Discussion, Review, DataPoint];
  public static readonly string[] NonAdminTypes = [Discussion, Review, DataPoint, Link];
  public static readonly string[] AdminTypes = [Discussion, Review, DataPoint, Link, Article, Blog];
}

public sealed record PostComposeTopicRatingDraft(string TopicId, int Rating);

public enum PostComposeCategoryKind
{
  Topic,
  Hashtag,
}

public sealed record PostComposeCategoryDraft(PostComposeCategoryKind Kind, string Value)
{
  public string UserContentValue => Value;
}

public sealed record PostComposeImageDraft(
    string ImageId,
    int OrderIndex,
    string? Caption = null,
    bool IsUploading = false,
    double? UploadProgress = null,
    string? UploadError = null,
    IUiLocalization? Localization = null)
{
  public string DisplayLabel => (Localization ?? UiLocalization.English).Format(
      UiMessageKey.NativeDotnetPostsImageNumber,
      ("count", OrderIndex + 1));

  public bool HasUploadError => !string.IsNullOrWhiteSpace(UploadError);

  public bool IsReady => !IsUploading && !HasUploadError;
}

public sealed record PostComposeRelatedUrlDraft(string Identifier);

public sealed record PostComposeValidation(
    bool CanPublish,
    string? Message = null,
    bool RequiresCaptcha = false);
