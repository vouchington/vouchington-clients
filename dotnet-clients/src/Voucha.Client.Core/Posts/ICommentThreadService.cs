using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Posts;

public partial interface ICommentThreadService
{
  Task<PostResponse> FetchPostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default);

  Task<PostThreadResponse> FetchPostDescendantsAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default);

  Task<PostThreadResponse> FetchPostAncestorsAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default);

  Task LockPostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default);

  Task UnlockPostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default);

  Task VotePostAsync(
      string postId,
      ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      Task.FromException(new NotSupportedException("Semantic post voting is not implemented by this comment service."));

  Task ClearPostVoteAsync(string postId, CancellationToken cancellationToken = default) =>
      Task.FromException(new NotSupportedException("Clearing a post vote is not implemented by this comment service."));

  Task BookmarkPostAsync(
      string postIdOrSlug,
      string predicate = "save",
      CancellationToken cancellationToken = default);

  Task UnbookmarkPostAsync(
      string postIdOrSlug,
      string predicate = "save",
      CancellationToken cancellationToken = default);

  Task ReportAsync(
      ReportBody body,
      CancellationToken cancellationToken = default);

  Task DeletePostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default);
}
