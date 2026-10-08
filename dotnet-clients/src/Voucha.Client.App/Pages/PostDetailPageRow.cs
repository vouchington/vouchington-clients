using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.FollowerDistributions;
using Voucha.Client.Core.Localization;

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
    UrlEmbedPreview? EmbedPreview = null)
{
  public bool CanInteractWithVotes => CanCreateVote || CanClearVote;

  public bool IsEligibleForFollowerDistribution =>
      Depth == 0 && FollowerDistributionEligibility.IsPublicTopLevelPost(Post.PostType, Post.ParentId, Post.Privacy, Post.Broadcast);

  public string Id => Post.Id;

  public string ProtocolPostType => Post.PostType ?? string.Empty;

  public UiText TitleText => string.IsNullOrWhiteSpace(Post.Title)
      ? UiTaxonomy.PostType(ProtocolPostType)
      : UiText.UserContent(Post.Title);

  public string LocalizedTitle => Localization.Resolve(TitleText);

  public string? ProvenanceLabel => PublicProvenanceLabels.Resolve(Post.Provenance, Localization);

  public bool HasProvenance => ProvenanceLabel is not null;

  public string BodyText => Post.Markdown ?? Post.Html ?? string.Empty;

  public AuthoredContentLanguage ContentLanguage => AuthoredContentLanguage.Resolve(
      Post.DeclaredLanguage, Post.LinguaRsDetectedLanguage);
  public bool? ContentIsRightToLeft => ContentLanguage.Direction is null ? null : ContentLanguage.Direction == AuthoredTextDirection.RightToLeft;
  public string? ContentFlowDirection => ContentLanguage.Direction?.ToString();
  public string? TitleFlowDirection => string.IsNullOrWhiteSpace(Post.Title) ? null : ContentFlowDirection;

  public string AuthorText => Post.DeletedAt is not null
      ? Localization.Localize(UiMessageKey.NativeDotnetPostsDeleted)
      : Post.IsAnonymous == true || Post.CreatedBy is null
          ? Localization.Localize(UiMessageKey.NativeDotnetPostsAnonymous)
          : Post.CreatedBy.Username
              ?? Post.CreatedById
              ?? Localization.Localize(UiMessageKey.NativeDotnetPostsUnknown);

  public string? AuthorAccountTypeLabel => Post.DeletedAt is not null || Post.IsAnonymous == true
      ? null
      : AccountTypeLabels.Resolve(Post.CreatedBy?.AccountType, Localization);

  public string CreatedAtText => Post.CreatedAt is DateTimeOffset createdAt
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
}
