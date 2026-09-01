using Voucha.Client.Core.Posts;

namespace Voucha.Client.App.Pages;

public sealed partial class PostDetailPageBinding
{
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
    var bookmarks = isRoot ? viewModel.RootBookmarks : viewModel.CommentBookmarks;
    var saved = bookmarks.TryGetValue(post.Id, out var actions) &&
        actions.TryGetValue("save", out var active) &&
        active;
    var election = isRoot ? viewModel.RootElection : TryGet(viewModel.CommentElections, post.Id);
    var vote = isRoot ? viewModel.RootElectionVote : TryGet(viewModel.CommentElectionVotes, post.Id);

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
    return viewModel.CommentHtml.TryGetValue(post.Id, out var html) ? html : null;
  }

  private static bool IsLocked(Post post) => post.LockedAt is not null;

  private static TValue? TryGet<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
      where TKey : notnull =>
      dictionary.TryGetValue(key, out var value) ? value : default;
}
