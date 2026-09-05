using Voucha.Client.Core.Api;
using Voucha.Client.Core.Contributions;
using Voucha.Client.Core.Localization;

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

  public async Task<bool> ReplyAsync(
      string parentPostId,
      string markdown,
      bool isAnonymous = false,
      string? turnstileToken = null,
      CancellationToken cancellationToken = default)
  {
    var posts = mutationPostsService ?? throw new InvalidOperationException("Replying requires a post mutation service.");
    var body = new CreatePostBody(
        "comment",
        string.Empty,
        markdown,
        turnstileToken,
        ParentId: parentPostId,
        RootId: rootPostId,
        IsAnonymous: isAnonymous);
    var canonicalIntent = ContributionRequestIdentity.CanonicalIntent(body);
    var commentScope = $"comment\u001f{parentPostId}";
    var idempotencyKey = contributionIdentity.KeyFor(commentScope, canonicalIntent);
    try
    {
      var created = await EmailVerificationGate.RunAsync(
          async () =>
          {
            await posts.CreatePostAsync(body, idempotencyKey, cancellationToken).ConfigureAwait(true);
            contributionIdentity.Complete(commentScope, canonicalIntent);
            return true;
          },
          ex =>
          {
            ErrorMessage = ex.ApiMessage ?? ex.Message;
            return false;
          }).ConfigureAwait(true);
      if (created)
      {
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
      }
      return created;
    }
    catch (VouchaApiException ex) when (ex.ErrorCode is "CONTRIBUTION_ADMISSION_IN_PROGRESS" or "IDEMPOTENCY_KEY_REUSED" or "CONTRIBUTION_QUOTA_EXCEEDED")
    {
      if (ex.ErrorCode == "IDEMPOTENCY_KEY_REUSED")
      {
        contributionIdentity.Abandon(commentScope, canonicalIntent);
      }
      ErrorMessage = ex.ErrorCode switch
      {
        "CONTRIBUTION_ADMISSION_IN_PROGRESS" => localization.Localize(UiMessageKey.NativeTaxonomyContributionAdmissionInProgress),
        "IDEMPOTENCY_KEY_REUSED" => localization.Localize(UiMessageKey.NativeTaxonomyContributionAdmissionIdempotencyMismatch),
        _ => localization.Localize(UiMessageKey.NativeTaxonomyContributionAdmissionCapacityUnavailable),
      };
      return false;
    }
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
