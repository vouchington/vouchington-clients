using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Posts;

namespace Voucha.Client.Core.Tests.Profiles;

internal sealed class RecordingPostsService : IPostsService
{
  public FetchPostsRequest? LastRequest { get; private set; }

  public int FetchPostsCallCount { get; private set; }

  public int? ThrowOnFetchPostsCall { get; set; }

  public Func<int, FetchPostsRequest, Task<PostsFeedResponse>>? FetchPostsAsyncOverride { get; set; }

  public Task<PostsFeedResponse> FetchFeedAsync(
      FetchPostsFeedRequest request,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

  public Task<PostsFeedResponse> FetchPostsAsync(
      FetchPostsRequest request,
      CancellationToken cancellationToken = default)
  {
    LastRequest = request;
    FetchPostsCallCount++;
    if (FetchPostsAsyncOverride is not null)
    {
      return FetchPostsAsyncOverride(FetchPostsCallCount, request);
    }

    if (ThrowOnFetchPostsCall == FetchPostsCallCount)
    {
      throw new InvalidOperationException("History failed");
    }

    var post = new Post("post-1", "review", "First review", "Body", "user-1");
    return Task.FromResult(new PostsFeedResponse(
        [new EntityReference(null, null, "post-1", null, null, null, null, null, null, null, null)],
        new PageInfo(null, false, null),
        new Dictionary<string, Post> { ["post-1"] = post },
        new Dictionary<string, User>(),
        new Dictionary<string, Community>()));
  }

  public Task<PostMutationResponse> CreatePostAsync(CreatePostBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  public Task<PostMutationResponse> CreateCommunityPostAsync(string communityIdOrSlug, CreatePostBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  public Task<PostMutationResponse> UpdatePostAsync(string postIdOrSlug, UpdatePostBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  public Task<PostMutationResponse> ArchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  public Task<PostMutationResponse> UnarchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  public Task VotePostAsync(string postId, int score, CancellationToken cancellationToken = default) => throw new NotSupportedException();
  public Task DeletePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class RecordingImageUploadService : IImageUploadService
{
  public string? ContentType { get; private set; }

  public long? ContentLength { get; private set; }

  public int FetchStateCount { get; private set; }

  public IReadOnlyList<ImageUploadState> StateResponses { get; init; } =
      [new ImageUploadState("image-1", "complete", null, true, false)];

  public Task<ImageUploadUrlResponse> CreateUploadUrlAsync(
      CreateImageUploadUrlBody body,
      CancellationToken cancellationToken = default)
  {
    ContentType = body.ContentType;
    ContentLength = body.ContentLength;
    return Task.FromResult(new ImageUploadUrlResponse(new ImageUploadUrl(
        "image-1",
        "https://upload.example.test/image-1",
        body.ContentType,
        DateTimeOffset.UtcNow)));
  }

  public Task UploadAsync(
      ImageUploadUrl upload,
      Stream content,
      long contentLength,
      CancellationToken cancellationToken = default) =>
      Task.CompletedTask;

  public Task<CompleteImageUploadResponse> CompleteAsync(
      string imageId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new CompleteImageUploadResponse(new ImageUpload("image-1", "complete")));

  public Task<ImageUploadStateResponse> FetchUploadStateAsync(
      string imageId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new ImageUploadStateResponse(StateResponses[Math.Min(FetchStateCount++, StateResponses.Count - 1)]));
}
