namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<Contributions.ContributionStatusResponse> FetchContributionStatusAsync(
      string? action = null,
      CancellationToken cancellationToken = default) =>
      SendAsync<Contributions.ContributionStatusResponse>(
          VouchaApiEndpoints.ContributionStatus(action), cancellationToken);

  public Task<PostsFeedResponse> FetchPostsAsync(
      FetchPostsRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostsFeedResponse>(
          VouchaApiEndpoints.AllPosts(request),
          cancellationToken);

  public Task<PostResponse> FetchPostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostResponse>(
          VouchaApiEndpoints.Post(postIdOrSlug),
          cancellationToken);

  public Task<PostThreadResponse> FetchPostDescendantsAsync(
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostThreadResponse>(VouchaApiEndpoints.PostDescendants(postId), cancellationToken);

  public Task<PostThreadResponse> FetchPostDescendantsPageAsync(
      string postId,
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostThreadResponse>(
          VouchaApiEndpoints.PostDescendants(postId, after, limit),
          cancellationToken);

  public Task<PostThreadResponse> FetchPostAncestorsAsync(
      string postId,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostThreadResponse>(
          VouchaApiEndpoints.PostAncestors(postId),
          cancellationToken);

  public Task<PostThreadResponse> FetchPostAncestorsPageAsync(
      string postId,
      string? after,
      int limit,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostThreadResponse>(
          VouchaApiEndpoints.PostAncestors(postId, after, limit),
          cancellationToken);

  public Task<PostMutationResponse> CreatePostAsync(
      CreatePostBody body,
      string idempotencyKey,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostMutationResponse>(
          VouchaApiEndpoints.CreatePost(body, idempotencyKey), cancellationToken);

  public Task<PostMutationResponse> CreateCommunityPostAsync(
      string communityIdOrSlug,
      CreatePostBody body,
      string idempotencyKey,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostMutationResponse>(
          VouchaApiEndpoints.CreateCommunityPost(communityIdOrSlug, body, idempotencyKey), cancellationToken);

  public Task<PostMutationResponse> UpdatePostAsync(
      string postIdOrSlug,
      UpdatePostBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostMutationResponse>(
          VouchaApiEndpoints.UpdatePost(postIdOrSlug, body),
          cancellationToken);

  public Task<PostMutationResponse> ArchivePostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostMutationResponse>(
          VouchaApiEndpoints.ArchivePost(postIdOrSlug),
          cancellationToken);

  public Task<PostMutationResponse> UnarchivePostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<PostMutationResponse>(
          VouchaApiEndpoints.UnarchivePost(postIdOrSlug),
          cancellationToken);

  public Task DeletePostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeletePost(postIdOrSlug), cancellationToken);

  public Task LockPostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.LockPost(postIdOrSlug), cancellationToken);

  public Task UnlockPostAsync(
      string postIdOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.UnlockPost(postIdOrSlug), cancellationToken);

  public Task BookmarkPostAsync(
      string postIdOrSlug,
      string predicate = "save",
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.Bookmark("post", postIdOrSlug, predicate), cancellationToken);

  public Task UnbookmarkPostAsync(
      string postIdOrSlug,
      string predicate = "save",
      CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.Unbookmark("post", postIdOrSlug, predicate), cancellationToken);

  public Task ReportAsync(ReportBody body, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.Report(body), cancellationToken);

  public Task<EntityRelationsResponse> FetchEntityRelationsAsync(
      EntityRelationsRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<EntityRelationsResponse>(
          VouchaApiEndpoints.EntityRelations(
              Require(request).EntityType,
              Require(request).EntityId,
              Require(request).Predicate,
              Require(request).ObjectType,
              Require(request).MinNetVoteScore,
              Require(request).PositiveNetVoteScore,
              Require(request).Limit,
              Require(request).Sort,
              Require(request).After),
          cancellationToken);

  public Task<EntityRelationResponse> CreateEntityRelationAsync(
      CreateEntityRelationRequest request,
      CancellationToken cancellationToken = default) =>
      SendAsync<EntityRelationResponse>(
          VouchaApiEndpoints.CreateEntityRelation(
              Require(request).EntityType,
              Require(request).EntityId,
              Require(request).Predicate,
              Require(request).ObjectType,
              new CreateEntityRelationBody(Require(request).ObjectId)),
          cancellationToken);

  public Task<ImageUploadUrlResponse> CreateImageUploadUrlAsync(
      CreateImageUploadUrlBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<ImageUploadUrlResponse>(
          VouchaApiEndpoints.CreateImageUploadUrl(body),
          cancellationToken);

  public Task<CompleteImageUploadResponse> CompleteImageUploadAsync(
      string imageId,
      CancellationToken cancellationToken = default) =>
      SendAsync<CompleteImageUploadResponse>(
          VouchaApiEndpoints.CompleteImageUpload(imageId),
          cancellationToken);

  public Task<ImageUploadStateResponse> FetchImageUploadStateAsync(
      string imageId,
      CancellationToken cancellationToken = default) =>
      SendAsync<ImageUploadStateResponse>(
          VouchaApiEndpoints.ImageUploadState(imageId),
          cancellationToken);

  public Task<TopicResponse> FetchTopicAsync(
      string topicIdOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<TopicResponse>(
          VouchaApiEndpoints.Topic(topicIdOrSlug),
          cancellationToken);

  public Task<ReferralProgramValidationInfoResponse> FetchReferralProgramValidationInfoAsync(
      string topicIdOrSlug,
      CancellationToken cancellationToken = default) =>
      SendAsync<ReferralProgramValidationInfoResponse>(
          VouchaApiEndpoints.ReferralProgramValidationInfo(topicIdOrSlug),
          cancellationToken);
}
