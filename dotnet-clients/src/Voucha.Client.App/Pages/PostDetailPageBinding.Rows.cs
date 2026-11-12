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
      yield return BuildRow(node.Post, depth, node.HasChildren || viewModel.DirectReplyCount(node.Id) > 0 || collapsed, collapsed);
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
    var canAct = CanCompose && !CommentPlaceholder.IsPlaceholder(post);
    var canReply = canAct && !isThreadLocked && !IsLocked(post);
    var canSave = canAct;
    var canReport = canAct && post.CreatedById is not null &&
        !string.Equals(post.CreatedById, sessionStore.Current.Identity?.Id, StringComparison.Ordinal);
    var canEdit = canAct && post.CanEditContent == true &&
        (sessionStore.Current.Identity?.Roles?.Contains("administrator", StringComparer.Ordinal) == true ||
            string.Equals(post.CreatedById, sessionStore.Current.Identity?.Id, StringComparison.Ordinal));
    var canDelete = canAct && post.CanDelete == true;
    var canLock = canAct && post.CanLock == true;
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
        CanVote && !CommentPlaceholder.IsPlaceholder(post) && !IsLocked(post),
        sessionStore.Current.CanClearPublicVote(vote?.Choice) && !CommentPlaceholder.IsPlaceholder(post) && !IsLocked(post),
        canSave,
        saved,
        canReply,
        canReport,
        canEdit,
        canDelete,
        canLock,
        IsLocked(post),
        CommentPlaceholder.IsPlaceholder(post) ? null : election?.VotesScoreNet,
        CommentPlaceholder.IsPlaceholder(post) ? null : election?.VotesCountUp,
        CommentPlaceholder.IsPlaceholder(post) ? null : election?.VotesCountDown,
        vote?.Choice,
        localization,
        CommentPlaceholder.IsPlaceholder(post) ? null : BodyHtml(post, isRoot),
        CommentPlaceholder.IsPlaceholder(post) ? null : viewModel.EmbedPreviewFor(post),
        sessionStore.Current.Identity?.Id,
        sessionStore.Current.Identity?.Roles?.Any(role => role is "administrator" or "moderator") == true)
    {
      DirectReplyCount = viewModel.DirectReplyCount(post.Id),
      CanToggleCollapse = !viewModel.IsCollapseBusy(post.Id),
      HasMoreSubtree = !isRoot && !collapsed && viewModel.HasMoreSubtree(post.Id),
      HasSubtreeError = viewModel.HasSubtreeError(post.Id),
    };
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
