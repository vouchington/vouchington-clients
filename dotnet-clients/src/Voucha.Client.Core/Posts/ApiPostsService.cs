using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Posts;

public sealed partial class ApiPostsService : IPostsService, ICommentThreadService
{
  private readonly VouchaApiClient client;

  public ApiPostsService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<PostsFeedResponse> FetchFeedAsync(
      FetchPostsFeedRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchPostsFeedAsync(request, cancellationToken);

  public Task<PostsFeedResponse> FetchPostsAsync(
      FetchPostsRequest request,
      CancellationToken cancellationToken = default) =>
      client.FetchPostsAsync(request, cancellationToken);

  public Task<PostResponse> FetchPostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      client.FetchPostAsync(postIdOrSlug, cancellationToken);

  public Task<PostThreadResponse> FetchPostDescendantsAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      client.FetchPostDescendantsAsync(postIdOrSlug, cancellationToken);

  public Task<PostThreadResponse> FetchPostAncestorsAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      client.FetchPostAncestorsAsync(postIdOrSlug, cancellationToken);

  public Task<PostMutationResponse> CreatePostAsync(
      CreatePostBody body,
      CancellationToken cancellationToken = default) =>
      client.CreatePostAsync(body, cancellationToken);

  public Task<PostMutationResponse> CreateCommunityPostAsync(
      string communityIdOrSlug,
      CreatePostBody body,
      CancellationToken cancellationToken = default) =>
      client.CreateCommunityPostAsync(communityIdOrSlug, body, cancellationToken);

  public Task<PostMutationResponse> UpdatePostAsync(
      string postIdOrSlug,
      UpdatePostBody body,
      CancellationToken cancellationToken = default) =>
      client.UpdatePostAsync(postIdOrSlug, body, cancellationToken);

  public Task<PostMutationResponse> ArchivePostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      client.ArchivePostAsync(postIdOrSlug, cancellationToken);

  public Task<PostMutationResponse> UnarchivePostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      client.UnarchivePostAsync(postIdOrSlug, cancellationToken);

  public Task LockPostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      client.LockPostAsync(postIdOrSlug, cancellationToken);

  public Task UnlockPostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      client.UnlockPostAsync(postIdOrSlug, cancellationToken);

  public Task VotePostAsync(
      string postId,
      ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      client.VotePostAsync(postId, choice, cancellationToken);

  public Task ClearPostVoteAsync(string postId, CancellationToken cancellationToken = default) =>
      client.ClearPostVoteAsync(postId, cancellationToken);

  public Task SetPostBookmarkAsync(
      string postId,
      string predicate,
      bool enabled,
      CancellationToken cancellationToken = default) =>
      client.SendAsync(
          enabled
              ? VouchaApiEndpoints.Bookmark("post", postId, predicate)
              : VouchaApiEndpoints.Unbookmark("post", postId, predicate),
          cancellationToken);

  public Task BookmarkPostAsync(
      string postIdOrSlug,
      string predicate = "save",
      CancellationToken cancellationToken = default) =>
      client.BookmarkPostAsync(postIdOrSlug, predicate, cancellationToken);

  public Task UnbookmarkPostAsync(
      string postIdOrSlug,
      string predicate = "save",
      CancellationToken cancellationToken = default) =>
      client.UnbookmarkPostAsync(postIdOrSlug, predicate, cancellationToken);

  public Task ReportAsync(ReportBody body, CancellationToken cancellationToken = default) =>
      client.ReportAsync(body, cancellationToken);

  public Task DeletePostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      client.DeletePostAsync(postIdOrSlug, cancellationToken);
}
