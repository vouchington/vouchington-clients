using System.Net;
using System.Net.Http;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostComposeLimitTests
{
  [Fact]
  public void ComposerIsUncappedBeforeTheLimitLoads()
  {
    var viewModel = NewViewModel(new LimitPostsService(Task.FromResult<int?>(null)));

    for (var index = 0; index < 21; index++)
    {
      Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft($"image-{index}", index)));
    }

    Assert.Null(viewModel.ImageLimit);
    Assert.Equal("21", viewModel.ImageCountLabel);
  }

  [Fact]
  public async Task FetchedLimitRejectsAnImagePastTheCap()
  {
    var viewModel = NewViewModel(new LimitPostsService(Task.FromResult<int?>(2)));
    await viewModel.LoadImageLimitAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("a", 0)));
    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("b", 1)));
    Assert.False(viewModel.TryAddImageDraft(new PostComposeImageDraft("c", 2)));

    Assert.Equal(2, viewModel.Images.Count);
    Assert.Equal("2/2", viewModel.ImageCountLabel);
    Assert.False(viewModel.CanUploadMoreImagesInBatch);
  }

  [Fact]
  public async Task UploadPastTheFetchedLimitShowsTheLocalizedMax()
  {
    var viewModel = NewViewModel(new LimitPostsService(Task.FromResult<int?>(1)), new RecordingImageUploadService());
    await viewModel.LoadImageLimitAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("a", 0)));

    using var content = new MemoryStream([1]);
    Assert.False(await viewModel.UploadImageAsync(content, "image/png", 1, cancellationToken: TestContext.Current.CancellationToken));

    Assert.Equal(
        UiLocalization.English.Format(UiMessageKey.SharedPostImageLimitMax, ("max", 1)),
        viewModel.ErrorMessage);
  }

  [Fact]
  public async Task FailedFetchLeavesTheComposerUncapped()
  {
    var viewModel = NewViewModel(new LimitPostsService(Task.FromException<int?>(new HttpRequestException("offline"))));
    await viewModel.LoadImageLimitAsync(TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ImageLimit);
    for (var index = 0; index < 21; index++)
    {
      Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft($"image-{index}", index)));
    }
  }

  [Fact]
  public async Task StaleLoadIsIgnored()
  {
    var slow = new TaskCompletionSource<int?>();
    var viewModel = NewViewModel(new LimitPostsService(slow.Task, Task.FromResult<int?>(5)));

    var first = viewModel.LoadImageLimitAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadImageLimitAsync(TestContext.Current.CancellationToken);
    slow.SetResult(1);
    await first;

    Assert.Equal(5, viewModel.ImageLimit);
  }

  [Fact]
  public async Task LowerFetchedLimitKeepsExistingDraftsOnEdit()
  {
    var viewModel = NewViewModel(new LimitPostsService(Task.FromResult<int?>(2)));
    for (var index = 0; index < 3; index++)
    {
      Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft($"image-{index}", index)));
    }
    await viewModel.LoadImageLimitAsync(TestContext.Current.CancellationToken);

    viewModel.SetImageCaption("image-0", "caption");

    Assert.Equal(["image-0", "image-1", "image-2"], viewModel.Images.Select(image => image.ImageId));
    Assert.False(viewModel.TryAddImageDraft(new PostComposeImageDraft("image-3", 3)));
  }

  [Fact]
  public async Task PublishMapsImageLimitCodeToItsMessage() =>
      await AssertPublishMapsCode("POST_IMAGE_LIMIT_REACHED", UiMessageKey.SharedPostImageLimit);

  [Fact]
  public async Task PublishMapsCategoryLimitCodeToItsMessage() =>
      await AssertPublishMapsCode("POST_CATEGORY_LIMIT_REACHED", UiMessageKey.SharedPostCategoryLimit);

  private static async Task AssertPublishMapsCode(string code, UiMessageKey key)
  {
    var failure = new VouchaApiException(HttpStatusCode.UnprocessableEntity, $"{{\"code\":\"{code}\"}}");
    var viewModel = NewViewModel(new LimitPostsService(Task.FromResult<int?>(null)) { Failure = failure });
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.TurnstileToken = "token";

    Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));

    Assert.Equal(UiLocalization.English.Localize(key), viewModel.ErrorMessage);
  }

  private static PostComposeViewModel NewViewModel(
      LimitPostsService service,
      IImageUploadService? imageUploadService = null) =>
      new(
          service,
          new AppConfig(new Uri("https://api.example.test"), "site-key", true),
          imageUploadService: imageUploadService);

  private sealed class LimitPostsService(params Task<int?>[] limits) : IPostsService
  {
    private int calls;

    public Exception? Failure { get; init; }

    public Task<int?> FetchPostImageLimitAsync(CancellationToken cancellationToken = default) =>
        limits[Math.Min(calls++, limits.Length - 1)];

    public Task<PostMutationResponse> CreatePostAsync(
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        Task.FromException<PostMutationResponse>(Failure ?? new NotSupportedException());

    public Task<PostsFeedResponse> FetchFeedAsync(FetchPostsFeedRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostsFeedResponse> FetchPostsAsync(FetchPostsRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UpdatePostAsync(string postIdOrSlug, UpdatePostBody body, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> ArchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UnarchivePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeletePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }
}
