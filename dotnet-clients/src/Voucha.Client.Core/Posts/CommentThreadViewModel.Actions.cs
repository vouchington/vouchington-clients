using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Posts;

public sealed partial class CommentThreadViewModel
{
  public Task ReloadAsync(CancellationToken cancellationToken = default) =>
      LoadInternalAsync(FocusedCommentId, cancellationToken);

  public async Task VotePostAsync(string postId, ElectionVoteChoice? choice, CancellationToken cancellationToken = default)
  {
    var shouldReload = await EmailVerificationGate.RunAsync(
        async () =>
        {
          if (choice is { } selected)
          {
            await postsService.VotePostAsync(postId, selected, cancellationToken).ConfigureAwait(true);
          }
          else
          {
            await postsService.ClearPostVoteAsync(postId, cancellationToken).ConfigureAwait(true);
          }
          return true;
        },
        ex =>
        {
          ErrorMessage = ex.ApiMessage ?? ex.Message;
          return false;
        }).ConfigureAwait(true);
    if (shouldReload)
    {
      await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }
  }

  public async Task ToggleSavePostAsync(string postId, bool isSaved, CancellationToken cancellationToken = default)
  {
    if (isSaved)
    {
      await postsService.UnbookmarkPostAsync(postId, predicate: "save", cancellationToken: cancellationToken).ConfigureAwait(true);
    }
    else
    {
      await postsService.BookmarkPostAsync(postId, predicate: "save", cancellationToken: cancellationToken).ConfigureAwait(true);
    }

    await ReloadAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task ReplyAsync(
      string parentPostId,
      string markdown,
      bool isAnonymous = false,
      CancellationToken cancellationToken = default)
  {
    var posts = mutationPostsService ?? throw new InvalidOperationException("Replying requires a post mutation service.");
    await posts.CreatePostAsync(
        new CreatePostBody("comment", string.Empty, markdown, ParentId: parentPostId, RootId: rootPostId, IsAnonymous: isAnonymous),
        cancellationToken).ConfigureAwait(true);
    await ReloadAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task EditAsync(string postId, string markdown, CancellationToken cancellationToken = default)
  {
    var posts = mutationPostsService ?? throw new InvalidOperationException("Editing requires a post mutation service.");
    await posts.UpdatePostAsync(postId, new UpdatePostBody(Markdown: markdown), cancellationToken).ConfigureAwait(true);
    await ReloadAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task ReportAsync(
      string postId,
      string reason,
      string? note = null,
      string? turnstileToken = null,
      CancellationToken cancellationToken = default)
  {
    await postsService.ReportAsync(new ReportBody("post", postId, reason, note, turnstileToken), cancellationToken).ConfigureAwait(true);
  }

  public async Task DeleteAsync(string postId, CancellationToken cancellationToken = default)
  {
    await postsService.DeletePostAsync(postId, cancellationToken).ConfigureAwait(true);
    if (postId == rootPostId || !TombstoneDescendant(postId))
    {
      await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }
  }

  public async Task LockAsync(string postId, CancellationToken cancellationToken = default)
  {
    await postsService.LockPostAsync(postId, cancellationToken).ConfigureAwait(true);
    await ReloadAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task UnlockAsync(string postId, CancellationToken cancellationToken = default)
  {
    await postsService.UnlockPostAsync(postId, cancellationToken).ConfigureAwait(true);
    await ReloadAsync(cancellationToken).ConfigureAwait(true);
  }
}
