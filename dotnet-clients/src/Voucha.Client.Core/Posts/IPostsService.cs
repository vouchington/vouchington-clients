using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Posts;

public interface IPostsService
{
  Task<PostsFeedResponse> FetchFeedAsync(
      FetchPostsFeedRequest request,
      CancellationToken cancellationToken = default);

  Task<PostsFeedResponse> FetchPostsAsync(
      FetchPostsRequest request,
      CancellationToken cancellationToken = default);

  Task<PostMutationResponse> CreatePostAsync(
      CreatePostBody body,
      string idempotencyKey,
      CancellationToken cancellationToken = default) =>
      Task.FromException<PostMutationResponse>(new NotSupportedException(
          "Post creation test doubles must implement the idempotency-aware operation."));

  Task<PostMutationResponse> CreateCommunityPostAsync(
      string communityIdOrSlug,
      CreatePostBody body,
      string idempotencyKey,
      CancellationToken cancellationToken = default) =>
      Task.FromException<PostMutationResponse>(new NotSupportedException(
          "Community post creation test doubles must implement the idempotency-aware operation."));

  Task<PostMutationResponse> UpdatePostAsync(
      string postIdOrSlug,
      UpdatePostBody body,
      CancellationToken cancellationToken = default);

  Task<PostMutationResponse> ArchivePostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default);

  Task<PostMutationResponse> UnarchivePostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default);

  Task VotePostAsync(
      string postId,
      ElectionVoteChoice choice,
      CancellationToken cancellationToken = default) =>
      Task.FromException(new NotSupportedException("Semantic post voting is not implemented by this posts service."));

  Task ClearPostVoteAsync(string postId, CancellationToken cancellationToken = default) =>
      Task.FromException(new NotSupportedException("Clearing a post vote is not implemented by this posts service."));

  Task SetPostBookmarkAsync(
      string postId,
      string predicate,
      bool enabled,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Post bookmark mutations are not implemented by this posts service.");

  Task DeletePostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default);
}
