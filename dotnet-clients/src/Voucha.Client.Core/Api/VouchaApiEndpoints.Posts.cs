namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest CreatePost(CreatePostBody body, string idempotencyKey) =>
      new(HttpMethod.Post, "/api/v1/posts") { Body = body, Headers = IdempotencyHeader(idempotencyKey) };

  public static ApiRequest UpdatePost(string postId, UpdatePostBody body) =>
      new(HttpMethod.Patch, $"/api/v1/posts/{Path(postId)}") { Body = body };

  public static ApiRequest DeletePost(string postId) =>
      new(HttpMethod.Delete, $"/api/v1/posts/{Path(postId)}");

  public static ApiRequest PostDescendants(
      string postIdOrSlug,
      string? after = null,
      int? limit = null) =>
      Get(
          $"/api/v1/posts/{Path(postIdOrSlug)}/descendants",
          Query(("limit", limit), ("after", after)));

  public static ApiRequest PostAncestors(string postIdOrSlug) =>
      Get($"/api/v1/posts/{Path(postIdOrSlug)}/ancestors");

  public static ApiRequest LockPost(string postId) =>
      new(HttpMethod.Post, $"/api/v1/posts/{Path(postId)}/lock");

  public static ApiRequest UnlockPost(string postId) =>
      new(HttpMethod.Delete, $"/api/v1/posts/{Path(postId)}/lock");

  public static ApiRequest ArchivePost(string postId) =>
      new(HttpMethod.Patch, $"/api/v1/posts/{Path(postId)}") { Body = new UpdatePostBody(Archive: true) };

  public static ApiRequest UnarchivePost(string postId) =>
      new(HttpMethod.Patch, $"/api/v1/posts/{Path(postId)}") { Body = new UpdatePostBody(Archive: false) };

  public static ApiRequest AddPostRating(string postId, AddPostRatingBody body) =>
      new(HttpMethod.Post, $"/api/v1/posts/{Path(postId)}/ratings") { Body = body };

  public static ApiRequest UpdatePostRating(string postId, string topicId, UpdatePostRatingBody body) =>
      new(HttpMethod.Patch, $"/api/v1/posts/{Path(postId)}/ratings/{Path(topicId)}") { Body = body };

  public static ApiRequest DeletePostRating(string postId, string topicId) =>
      new(HttpMethod.Delete, $"/api/v1/posts/{Path(postId)}/ratings/{Path(topicId)}");

  public static ApiRequest SetPostImages(string postId, SetPostImagesBody body) =>
      new(HttpMethod.Put, $"/api/v1/posts/{Path(postId)}/images") { Body = body };

  public static ApiRequest CreateCommunityPost(string communityIdOrSlug, CreatePostBody body, string idempotencyKey) =>
      new(HttpMethod.Post, $"/api/v1/communities/{Path(communityIdOrSlug)}/posts") { Body = body, Headers = IdempotencyHeader(idempotencyKey) };

  internal static IReadOnlyDictionary<string, string> IdempotencyHeader(string key) =>
      !string.IsNullOrWhiteSpace(key)
          ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Idempotency-Key"] = key }
          : throw new ArgumentException("An Idempotency-Key is required.", nameof(key));

  public static ApiRequest EntityRelations(
      string entityType,
      string entityId,
      string predicate,
      string objectType,
      int? minNetVoteScore = null,
      bool? positiveNetVoteScore = null,
      int limit = 100,
      string? sort = "best",
      string? after = null) =>
      Get(
          $"/api/v1/entity-relations/{Path(entityType)}/{Path(entityId)}/{Path(predicate)}/{Path(objectType)}",
          Query(
              ("minNetVoteScore", minNetVoteScore),
              ("positiveNetVoteScore", Bool(positiveNetVoteScore)),
              ("limit", limit),
              ("sort", sort),
              ("after", after)));

  public static ApiRequest CreateEntityRelation(
      string entityType,
      string entityId,
      string predicate,
      string objectType,
      CreateEntityRelationBody body) =>
      new(
          HttpMethod.Post,
          $"/api/v1/entity-relations/{Path(entityType)}/{Path(entityId)}/{Path(predicate)}/{Path(objectType)}")
      {
        Body = body,
      };

  public static ApiRequest CreateImageUploadUrl(CreateImageUploadUrlBody body) =>
      new(HttpMethod.Post, "/api/v1/images/upload-url") { Body = body };

  public static ApiRequest CompleteImageUpload(string imageId) =>
      new(HttpMethod.Post, $"/api/v1/images/{Path(imageId)}/completions");

  public static ApiRequest ImageUploadState(string imageId) =>
      Get($"/api/v1/images/{Path(imageId)}/upload-state");

  public static ApiRequest UpdatePodcastPlaybackPosition(
      string rssFeedItemId,
      double positionSeconds,
      bool completed) =>
      new(HttpMethod.Put, $"/api/v1/podcast-episodes/{Path(rssFeedItemId)}/playback-position")
      {
        Body = new UpdatePodcastPlaybackPositionBody(positionSeconds, completed),
      };

  public static ApiRequest PodcastPlaybackPosition(string rssFeedItemId) =>
      Get($"/api/v1/podcast-episodes/{Path(rssFeedItemId)}/playback-position");
}
