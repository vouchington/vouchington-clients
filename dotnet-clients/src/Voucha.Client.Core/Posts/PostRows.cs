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
    UrlEmbedPreview? EmbedPreview = null)
{
  private IUiLocalization L => Localization ?? UiLocalization.English;

  public string Title => L.Resolve(TitleText);

  public string Subtitle => L.Resolve(SubtitleText);

  public string LocalizedPostType => L.Resolve(PostTypeText);

  public bool HasVoteCounts => VoteCountUp is not null || VoteCountDown is not null;

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
        string.IsNullOrWhiteSpace(post.Title)
            ? UiText.Localized(
                UiMessageKey.NativeDotnetPostsUntitledPostType,
                ("type", localizer.Resolve(type)))
            : UiText.Verbatim(post.Title!),
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
        EmbedPreview: EmbedFor(post.Id, embeds));
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

  private static UiText PostTypeText(string? value) => UiTaxonomy.PostType(value);
}
