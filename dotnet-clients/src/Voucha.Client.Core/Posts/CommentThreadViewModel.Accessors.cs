using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Posts;

public sealed partial class CommentThreadViewModel
{
  private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> EmptyBookmarks =
      new Dictionary<string, IReadOnlyDictionary<string, bool>>(StringComparer.Ordinal);

  private static readonly IReadOnlyDictionary<string, PostElection> EmptyPostElections =
      new Dictionary<string, PostElection>(StringComparer.Ordinal);

  private static readonly IReadOnlyDictionary<string, ElectionVote> EmptyElectionVotes =
      new Dictionary<string, ElectionVote>(StringComparer.Ordinal);

  public PostResponse? RootResponse => rootPostResponse;

  public PostThreadResponse? DescendantsResponse => descendantsResponse;

  public PostThreadResponse? AncestorsResponse => ancestorsResponse;

  public PostElection? RootElection => rootPostResponse?.PostElection;

  public ElectionVote? RootElectionVote => rootPostResponse?.ElectionVote;

  public IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> RootBookmarks =>
      rootPostResponse?.Bookmarks ?? EmptyBookmarks;

  public IReadOnlyDictionary<string, PostElection> CommentElections =>
      descendantsResponse?.PostElections ?? EmptyPostElections;

  public IReadOnlyDictionary<string, ElectionVote> CommentElectionVotes =>
      descendantsResponse?.ElectionVotes ?? EmptyElectionVotes;

  public IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> CommentBookmarks =>
      descendantsResponse?.Bookmarks ?? EmptyBookmarks;

  public IReadOnlyDictionary<string, string> CommentHtml =>
      descendantsResponse?.MarkdownToHtml ?? new Dictionary<string, string>(StringComparer.Ordinal);

  private static string? NormalizeFocusedCommentId(string? commentId) =>
      string.IsNullOrWhiteSpace(commentId) ? null : commentId.Trim();

  private static string NormalizeSort(string? value) =>
      string.Equals(value, CommentThreadSorts.New, StringComparison.Ordinal) ? CommentThreadSorts.New : CommentThreadSorts.Best;

  public static string BuildCollapseStateKey(string rootPostId, string? currentUserId = null) =>
      string.IsNullOrWhiteSpace(currentUserId)
          ? $"comments-collapsed:{rootPostId}"
          : $"comments-collapsed:{rootPostId}:{currentUserId}";

  private static string? EntityId(EntityReference result) => result.EntityId ?? result.Id;
}
