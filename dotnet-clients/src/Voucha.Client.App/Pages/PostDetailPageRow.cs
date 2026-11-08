using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.FollowerDistributions;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;

namespace Voucha.Client.App.Pages;

public sealed record PostDetailPageRow(
    Post Post,
    string RootPostType,
    string RootPostId,
    int Depth,
    bool IsFocused,
    bool HasChildren,
    bool IsCollapsed,
    bool CanCreateVote,
    bool CanClearVote,
    bool CanSave,
    bool IsSaved,
    bool CanReply,
    bool CanReport,
    bool CanEdit,
    bool CanDelete,
    bool CanLock,
    bool IsLocked,
    double? VoteScoreNet,
    int? VoteCountUp,
    int? VoteCountDown,
    ElectionVoteChoice? CurrentVoteChoice,
    IUiLocalization Localization,
    string? BodyHtml = null,
    UrlEmbedPreview? EmbedPreview = null,
    string? ViewerId = null,
    bool IsStaffViewer = false)
{
  public bool CanInteractWithVotes => CanCreateVote || CanClearVote;

  public bool IsEligibleForFollowerDistribution =>
      Depth == 0 && FollowerDistributionEligibility.IsPublicTopLevelPost(Post.PostType, Post.ParentId, Post.Privacy, Post.Broadcast);

  public string Id => Post.Id;

  public string ProtocolPostType => Post.PostType ?? string.Empty;

  public UiText TitleText => IsPlaceholder ? CommentPlaceholder.Label(Post) : string.IsNullOrWhiteSpace(Post.Title)
      ? UiTaxonomy.PostType(ProtocolPostType)
      : UiText.UserContent(Post.Title);

  public string LocalizedTitle => Localization.Resolve(TitleText);

  public string? ProvenanceLabel => IsPlaceholder ? null : PublicProvenanceLabels.Resolve(Post.Provenance, Localization);

  public bool HasProvenance => ProvenanceLabel is not null;

  public string? StaffChannelLabel => StaffProvenanceLabels.Channel(Post.StaffProvenance, IsStaffViewer && !IsPlaceholder, Localization);
  public bool HasStaffChannel => StaffChannelLabel is not null;
  public string? StaffClientLabel => StaffProvenanceLabels.Client(Post.StaffProvenance, IsStaffViewer && !IsPlaceholder, true, Localization);
  public bool HasStaffClient => StaffClientLabel is not null;
  public string? StaffVerificationLabel => StaffProvenanceLabels.Verification(
      Post.StaffProvenance, IsStaffViewer && !IsPlaceholder, true, Localization);
  public bool HasStaffVerification => StaffVerificationLabel is not null;

  public PostContentPresentation ContentPresentation => PostContentPolicy.For(Post, ViewerId, IsStaffViewer);
  public bool ShowBodyContent => !IsPlaceholder && !IsCollapsed && ContentPresentation is PostContentPresentation.Content or PostContentPresentation.UnderReviewForStaff;
  public bool CanQuote => CanReply && ShowBodyContent;
  public bool ShowAppealsAction => !IsPlaceholder && ContentPresentation == PostContentPresentation.RemovedForAuthor;
  public bool ShowNotice => IsPlaceholder || ContentPresentation is PostContentPresentation.RemovedForAuthor or PostContentPresentation.Unavailable;
  public string? LocalizedNotice => IsPlaceholder ? Localization.Resolve(CommentPlaceholder.Label(Post)) : ContentPresentation switch
  {
    PostContentPresentation.RemovedForAuthor => Localization.Localize(UiMessageKey.NativeAuthoredPostRemoved),
    PostContentPresentation.Unavailable => Localization.Localize(UiMessageKey.NativeAuthoredPostUnavailable),
    _ => null,
  };
  public string? LocalizedRemovalReason => ShowAppealsAction
      ? string.IsNullOrWhiteSpace(Post.ClearanceReason)
          ? Localization.Localize(UiMessageKey.NativeAuthoredPostNoAdditionalReason)
          : Localization.Resolve(UiText.ProtocolValue(Post.ClearanceReason))
      : null;
  public string? LocalizedReviewBadge => !IsPlaceholder && ContentPresentation == PostContentPresentation.UnderReviewForStaff
      ? Localization.Localize(UiMessageKey.NativeDotnetModerationInReview)
      : null;
  public bool HasEmbedPreview => !IsPlaceholder && !IsCollapsed && PostContentPolicy.CanShowMedia(Post.ClearanceStatus, IsStaffViewer) && EmbedPreview?.HasPreview == true;
  public UrlEmbedPreview? VisibleEmbedPreview => HasEmbedPreview ? EmbedPreview : null;
  public bool CanPlayEmbed => HasEmbedPreview && EmbedPreview?.CanPlay == true;
  public bool HasEmbedSource => HasEmbedPreview && EmbedPreview?.HasSource == true;

  public string BodyText => IsPlaceholder ? string.Empty : Post.Markdown ?? Post.Html ?? string.Empty;

  public AuthoredContentLanguage ContentLanguage => AuthoredContentLanguage.Resolve(
      Post.DeclaredLanguage, Post.LinguaRsDetectedLanguage);
  public bool? ContentIsRightToLeft => ContentLanguage.Direction is null ? null : ContentLanguage.Direction == AuthoredTextDirection.RightToLeft;
  public string? ContentFlowDirection => ContentLanguage.Direction?.ToString();
  public string? TitleFlowDirection => string.IsNullOrWhiteSpace(Post.Title) ? null : ContentFlowDirection;

  public string AuthorText => IsPlaceholder ? string.Empty : Post.DeletedAt is not null
      ? Localization.Localize(UiMessageKey.NativeDotnetPostsDeleted)
      : Post.IsAnonymous == true || Post.CreatedBy is null
          ? Localization.Localize(UiMessageKey.NativeDotnetPostsAnonymous)
          : Post.CreatedBy.Username
              ?? Post.CreatedById
              ?? Localization.Localize(UiMessageKey.NativeDotnetPostsUnknown);

  public TopicImagePlacement? AuthorImagePlacement =>
      !IsPlaceholder && Post.DeletedAt is null && Post.IsAnonymous != true ? Post.CreatedBy?.ProfileImagePlacement : null;

  public bool HasAuthorImage => AuthorImagePlacement is not null;

  public string? AuthorAccountTypeLabel => IsPlaceholder || Post.DeletedAt is not null || Post.IsAnonymous == true
      ? null
      : AccountTypeLabels.Resolve(Post.CreatedBy?.AccountType, Localization);

  public string CreatedAtText => !IsPlaceholder && Post.CreatedAt is DateTimeOffset createdAt
      ? Localization.FormatDateTime(createdAt, TimeZoneInfo.Local)
      : string.Empty;

  public string PermalinkPath => Depth == 0
      ? $"/{PostTypeSlug(RootPostType)}/{RootPostId}"
      : $"/{PostTypeSlug(RootPostType)}/{RootPostId}/comment/{Post.Id}";

  public string PermalinkText => Depth == 0
      ? LocalizedTitle
      : Localization.Localize(UiMessageKey.NativeDotnetPostsViewComment);

  public Thickness Indent => new(Depth * 20, 0, 0, 0);

  public string CollapseGlyph => IsCollapsed ? "▸" : "▾";

  private static string PostTypeSlug(string? postType) =>
      postType switch
      {
        "data_point" => "data-point",
        "blog_post" => "blog-post",
        "discussion" => "discussion",
        "review" => "review",
        "article" => "article",
        "link" => "link",
        _ => "post",
      };

  public bool IsPlaceholder => CommentPlaceholder.IsPlaceholder(Post);
  public bool ShowAuthor => !IsPlaceholder;
  public bool ShowActions => !IsPlaceholder && !IsCollapsed;
  public int DirectReplyCount { get; init; }
  public bool CanToggleCollapse { get; init; } = true;
  public bool HasMoreSubtree { get; init; }
  public bool HasSubtreeError { get; init; }
  public bool ShowCollapsedReplyCount => IsCollapsed;
  public string CollapsedReplyCountText => Localization.FormatNumber(DirectReplyCount);
  public string SubtreePaginationLabel => Localization.Localize(HasSubtreeError
      ? UiMessageKey.NativeCommonRetry
      : UiMessageKey.NativeSwiftCommonLoadMore);
}
