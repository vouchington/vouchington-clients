using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Posts;

namespace Voucha.Client.App.Pages;

public sealed partial class PostDetailPageBinding
{
  private IReadOnlyDictionary<string, PostElection> commentElections = new Dictionary<string, PostElection>();
  private IReadOnlyDictionary<string, ElectionVote> commentElectionVotes = new Dictionary<string, ElectionVote>();
  private IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> commentBookmarks =
      new Dictionary<string, IReadOnlyDictionary<string, bool>>();
  private IReadOnlyDictionary<string, string> commentHtml = new Dictionary<string, string>();

  private void RefreshCommentMetadata()
  {
    commentElections = viewModel.CommentElections;
    commentElectionVotes = viewModel.CommentElectionVotes;
    commentBookmarks = viewModel.CommentBookmarks;
    commentHtml = viewModel.CommentHtml;
  }

  private static bool ContainsComment(IReadOnlyList<CommentThreadNodeViewModel> nodes, string id) =>
      nodes.Any(node => string.Equals(node.Id, id, StringComparison.Ordinal) || ContainsComment(node.Children, id));

  private IEnumerable<PostDetailPageRow> BuildRows(
      IReadOnlyList<CommentThreadNodeViewModel> nodes,
      int depth)
  {
    foreach (var node in nodes)
    {
      var collapsed = viewModel.CollapsedCommentIds.Contains(node.Id);
      yield return BuildRow(node.Post, depth, node.HasChildren, collapsed);
      if (!collapsed)
      {
        foreach (var child in BuildRows(node.Children, depth + 1))
        {
          yield return child;
        }
      }
    }
  }

  private PostDetailPageRow BuildRow(Post post, int depth, bool hasChildren, bool collapsed)
  {
    var isRoot = post.Id == viewModel.RootPostId;
    var isThreadLocked = viewModel.RootPost?.LockedAt is not null;
    var isFocused = string.Equals(viewModel.FocusedCommentId, post.Id, StringComparison.Ordinal);
    var canReply = CanCompose && !isThreadLocked && !IsLocked(post);
    var canSave = CanCompose;
    var canReport = CanCompose && post.CreatedById is not null &&
        !string.Equals(post.CreatedById, sessionStore.Current.Identity?.Id, StringComparison.Ordinal);
    var canEdit = CanCompose && post.CanEditContent == true &&
        (sessionStore.Current.Identity?.Roles?.Contains("administrator", StringComparer.Ordinal) == true ||
            string.Equals(post.CreatedById, sessionStore.Current.Identity?.Id, StringComparison.Ordinal));
    var canDelete = CanCompose && post.CanDelete == true;
    var canLock = CanCompose && post.CanLock == true;
    var bookmarks = isRoot ? viewModel.RootBookmarks : commentBookmarks;
    var saved = bookmarks.TryGetValue(post.Id, out var actions) &&
        actions.TryGetValue("save", out var active) &&
        active;
    var election = isRoot ? viewModel.RootElection : TryGet(commentElections, post.Id);
    var vote = isRoot ? viewModel.RootElectionVote : TryGet(commentElectionVotes, post.Id);

    return new PostDetailPageRow(
        post,
        viewModel.RootPost?.PostType ?? string.Empty,
        viewModel.RootPostId,
        depth,
        isFocused,
        hasChildren,
        collapsed,
        CanVote && !IsLocked(post),
        sessionStore.Current.CanClearPublicVote(vote?.Choice) && !IsLocked(post),
        canSave,
        saved,
        canReply,
        canReport,
        canEdit,
        canDelete,
        canLock,
        IsLocked(post),
        election?.VotesScoreNet,
        election?.VotesCountUp,
        election?.VotesCountDown,
        vote?.Choice,
        localization,
        BodyHtml(post, isRoot),
        viewModel.EmbedPreviewFor(post));
  }

  private string? BodyHtml(Post post, bool isRoot)
  {
    if (isRoot) return viewModel.RootHtml;
    return commentHtml.TryGetValue(post.Id, out var html) ? html : null;
  }

  private static bool IsLocked(Post post) => post.LockedAt is not null;

  private static TValue? TryGet<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
      where TKey : notnull =>
      dictionary.TryGetValue(key, out var value) ? value : default;
}
