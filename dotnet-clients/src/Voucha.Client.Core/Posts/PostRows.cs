using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.FollowerDistributions;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Posts;

public sealed record PostRow(
    string Id,
    UiText TitleText,
    UiText SubtitleText,
    string? Body,
    string? BodyHtml,
    string ProtocolPostType,
    UiText PostTypeText,
    string? RootId = null,
    double? VoteScoreNet = null,
    int? VoteCountUp = null,
    int? VoteCountDown = null,
    ElectionVoteChoice? CurrentVoteChoice = null,
    bool IsSaved = false,
    bool IsHidden = false,
    IUiLocalization? Localization = null,
    string? CreatedById = null,
    string? Broadcast = null,
    string? Privacy = null,
    string? ParentId = null,
    UrlEmbedPreview? EmbedPreview = null,
    string? DeclaredLanguage = null,
    string? DetectedLanguage = null,
    bool HasAuthoredTitle = false,
    AccountType? AuthorAccountType = null,
    PublicContentProvenance? Provenance = null,
    string? ViewerId = null,
    bool IsStaffViewer = false,
    string? ClearanceStatus = null,
    string? ClearanceReason = null)
{
  private IUiLocalization L => Localization ?? UiLocalization.English;

  public string Title => L.Resolve(TitleText);

  public string Subtitle => L.Resolve(SubtitleText);

  public string LocalizedPostType => L.Resolve(PostTypeText);

  public string? AuthorAccountTypeLabel => AccountTypeLabels.Resolve(AuthorAccountType, L);

  public AuthoredContentLanguage ContentLanguage => AuthoredContentLanguage.Resolve(DeclaredLanguage, DetectedLanguage);
  public bool? ContentIsRightToLeft => ContentLanguage.Direction is null ? null : ContentLanguage.Direction == AuthoredTextDirection.RightToLeft;
  public string? TitleFlowDirection => HasAuthoredTitle ? ContentLanguage.Direction?.ToString() : null;

  public bool HasVoteCounts => VoteCountUp is not null || VoteCountDown is not null;

  public bool HasProvenance => Provenance is not null;

  public string? LocalizedProvenanceLabel => PublicProvenanceLabels.Resolve(Provenance, L);

  public PostContentPresentation ContentPresentation =>
      PostContentPolicy.For(ClearanceStatus, CreatedById, ViewerId, IsStaffViewer);

  public bool ShowBodyContent => ContentPresentation is PostContentPresentation.Content or PostContentPresentation.UnderReviewForStaff;
  public bool ShowAppealsAction => ContentPresentation == PostContentPresentation.RemovedForAuthor;
  public bool ShowNotice => ContentPresentation is PostContentPresentation.RemovedForAuthor or PostContentPresentation.Unavailable;
  public bool ShowEmbed => PostContentPolicy.CanShowMedia(ClearanceStatus, IsStaffViewer) && EmbedPreview?.HasPreview == true;
  public bool CanPlayEmbed => ShowEmbed && EmbedPreview?.CanPlay == true;
  public bool HasEmbedSource => ShowEmbed && EmbedPreview?.HasSource == true;
  public string? LocalizedNotice => ContentPresentation switch
  {
    PostContentPresentation.RemovedForAuthor => L.Localize(UiMessageKey.NativeAuthoredPostRemoved),
    PostContentPresentation.Unavailable => L.Localize(UiMessageKey.NativeAuthoredPostUnavailable),
    _ => null,
  };
  public string? LocalizedRemovalReason => ShowAppealsAction
      ? string.IsNullOrWhiteSpace(ClearanceReason)
          ? L.Localize(UiMessageKey.NativeAuthoredPostNoAdditionalReason)
          : L.Resolve(UiText.ProtocolValue(ClearanceReason))
      : null;
  public string? LocalizedReviewBadge => ContentPresentation == PostContentPresentation.UnderReviewForStaff
      ? L.Localize(UiMessageKey.NativeDotnetModerationInReview)
      : null;

  public bool IsEligibleForFollowerDistribution =>
      FollowerDistributionEligibility.IsPublicTopLevelPost(ProtocolPostType, ParentId, Privacy, Broadcast);

  public string SaveActionLabel => L.Localize(IsSaved ? UiMessageKey.NativeDotnetDynamicUnsave : UiMessageKey.NativeDotnetDynamicSave);

  public string HideActionLabel => L.Localize(IsHidden ? UiMessageKey.NativeDotnetDynamicUnhide : UiMessageKey.NativeDotnetDynamicHide);
}

public static class PostRows
{
  public static PostRow From(
      Post post,
      IReadOnlyDictionary<string, PostElection>? elections,
      IReadOnlyDictionary<string, ElectionVote>? votes,
      IReadOnlyDictionary<string, BookmarkPredicates>? bookmarks,
      IUiLocalization? localization = null) =>
      From(post, elections, votes, bookmarks, embeds: null, localization);

  public static PostRow From(
      Post post,
      IReadOnlyDictionary<string, PostElection>? elections,
      IReadOnlyDictionary<string, ElectionVote>? votes,
      IReadOnlyDictionary<string, BookmarkPredicates>? bookmarks,
      IReadOnlyDictionary<string, UrlEmbed>? embeds,
      IUiLocalization? localization = null)
  {
    ArgumentNullException.ThrowIfNull(post);

    PostElection? election = null;
    ElectionVote? vote = null;
    elections?.TryGetValue(post.Id, out election);
    votes?.TryGetValue(post.Id, out vote);
    var localizer = localization ?? UiLocalization.English;
    var type = PostTypeText(post.PostType);

    return new PostRow(
        post.Id,
        Normalize(post.Title) is not { } title
            ? UiText.Localized(
                UiMessageKey.NativeDotnetPostsUntitledPostType,
                ("type", localizer.Resolve(type)))
            : UiText.Verbatim(title),
        post.CreatedById is null
            ? type
            : UiText.Localized(
                UiMessageKey.NativeDotnetPostsPostTypeByAuthor,
                ("type", localizer.Resolve(type)),
                ("author", post.CreatedById)),
        post.Markdown,
        BodyHtml: post.Html,
        post.PostType ?? "post",
        type,
        post.RootId,
        election?.VotesScoreNet,
        election?.VotesCountUp,
        election?.VotesCountDown,
        vote?.Choice,
        BookmarkSidecar.IsActive(bookmarks, post.Id, BookmarkPredicate.Save),
        BookmarkSidecar.IsActive(bookmarks, post.Id, BookmarkPredicate.Hide),
        Localization: localizer,
        CreatedById: post.CreatedById,
        Broadcast: post.Broadcast,
        Privacy: post.Privacy,
        ParentId: post.ParentId,
        EmbedPreview: EmbedFor(post.Id, embeds),
        DeclaredLanguage: post.DeclaredLanguage,
        DetectedLanguage: post.LinguaRsDetectedLanguage,
        HasAuthoredTitle: Normalize(post.Title) is not null,
        AuthorAccountType: post.IsAnonymous == true || post.DeletedAt is not null ? null : post.CreatedBy?.AccountType,
        Provenance: post.Provenance,
        ClearanceStatus: post.ClearanceStatus,
        ClearanceReason: post.ClearanceReason);
  }

  public static PostRow From(
      Post post,
      IReadOnlyDictionary<string, PostElection>? elections,
      IReadOnlyDictionary<string, ElectionVote>? votes,
      IReadOnlyDictionary<string, BookmarkPredicates>? bookmarks,
      IReadOnlyDictionary<string, string>? markdownToHtml,
      IUiLocalization? localization = null) =>
      From(post, elections, votes, bookmarks, markdownToHtml, embeds: null, localization);

  public static PostRow From(
      Post post,
      IReadOnlyDictionary<string, PostElection>? elections,
      IReadOnlyDictionary<string, ElectionVote>? votes,
      IReadOnlyDictionary<string, BookmarkPredicates>? bookmarks,
      IReadOnlyDictionary<string, string>? markdownToHtml,
      IReadOnlyDictionary<string, UrlEmbed>? embeds,
      IUiLocalization? localization = null)
  {
    var row = From(post, elections, votes, bookmarks, embeds, localization);
    return row with
    {
      BodyHtml = markdownToHtml is not null && markdownToHtml.TryGetValue(post.Id, out var html)
          ? html
          : post.Html,
    };
  }

  private static UrlEmbedPreview? EmbedFor(string postId, IReadOnlyDictionary<string, UrlEmbed>? embeds) =>
      embeds is not null && embeds.TryGetValue(postId, out var embed)
          ? UrlEmbedPreviews.From(embed)
          : null;

  private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  private static UiText PostTypeText(string? value) => UiTaxonomy.PostType(value);
}
