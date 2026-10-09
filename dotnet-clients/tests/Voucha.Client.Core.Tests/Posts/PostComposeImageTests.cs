using System.Net.Http;
using System.Text;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed partial class PostComposeImageTests
{
  private const long MaxImageUploadBytes = 50L * 1024 * 1024;

  [Fact]
  public void ImageListOperationsRespectMaxCapReorderAndCaptionChanges()
  {
    var viewModel = NewViewModel();

    Assert.False(viewModel.CanAddImages);

    for (var index = 0; index < 20; index++)
    {
      Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft($"image-{index}", index, $"Caption {index}")));
    }

    Assert.False(viewModel.TryAddImageDraft(new PostComposeImageDraft("image-20", 20, "Overflow")));
    Assert.Equal(20, viewModel.Images.Count);
    Assert.Equal("20/20", viewModel.ImageCountLabel);
    Assert.False(viewModel.CanAddImages);

    viewModel.MoveImage(19, 0);
    viewModel.SetImageCaption("image-19", "Updated caption");
    viewModel.RemoveImage("image-10");

    Assert.Equal("image-19", viewModel.Images[0].ImageId);
    Assert.Equal("Updated caption", viewModel.Images[0].Caption);
    Assert.Equal(19, viewModel.Images.Count);
    Assert.Equal(Enumerable.Range(0, 19), viewModel.Images.Select(image => image.OrderIndex));
  }

  [Fact]
  public async Task UploadImageAsyncWithoutServiceSurfacesError()
  {
    var viewModel = NewViewModel();

    using var content = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    Assert.False(await viewModel.UploadImageAsync(content, "image/png", content.Length, cancellationToken: TestContext.Current.CancellationToken));

    Assert.Equal("Image uploads are unavailable.", viewModel.ErrorMessage);
    Assert.True(viewModel.HasError);
    Assert.False(viewModel.CanAddImages);
  }

  [Fact]
  public async Task UploadImageAsyncAddsCompletedImageAndPublishesIt()
  {
    var uploadService = new RecordingImageUploadService
    {
      StateResponses = [new ImageUploadState("image-1", "complete", null, false, false)],
    };
    var postsService = new RecordingPostsService();
    var viewModel = NewViewModel(postsService, uploadService);

    using var content = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    Assert.True(await viewModel.UploadImageAsync(content, "image/png", content.Length, "Caption", TestContext.Current.CancellationToken));

    Assert.Equal("image/png", uploadService.ContentType);
    Assert.Equal(3, uploadService.ContentLength);
    Assert.Equal(1, uploadService.FetchStateCount);
    Assert.Single(viewModel.Images);
    Assert.Equal("image-1", viewModel.Images[0].ImageId);
    Assert.Equal("Caption", viewModel.Images[0].Caption);
    Assert.Equal(1d, viewModel.Images[0].UploadProgress);
    Assert.Null(viewModel.Images[0].UploadError);
    Assert.True(viewModel.CanAddImages);
    Assert.False(viewModel.IsUploadingImages);

    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.TurnstileToken = "token";

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Equal("image-1", Assert.Single(postsService.Body?.Images ?? []).ImageId);
    Assert.Equal("Caption", postsService.Body?.Images?[0].Caption);
  }

  [Fact]
  public async Task UploadImageAsyncRetriesTransientCompleteFailuresWithinThePollBudget()
  {
    var uploadService = new RecordingImageUploadService
    {
      CompleteTransientFailuresBeforeSuccess = 1,
      StateResponses = [new ImageUploadState("image-1", "complete", null, false, false)],
    };
    var viewModel = NewViewModel(imageUploadService: uploadService, imageUploadPollInterval: TimeSpan.Zero, imageUploadPollAttempts: 2);

    using var content = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    Assert.True(await viewModel.UploadImageAsync(content, "image/png", content.Length, cancellationToken: TestContext.Current.CancellationToken));

    Assert.Equal(2, uploadService.CompleteCount);
    Assert.Equal(1, uploadService.FetchStateCount);
    var image = Assert.Single(viewModel.Images);
    Assert.True(image.IsReady);
    Assert.Null(image.UploadError);
  }

  [Fact]
  public async Task UploadImageAsyncRetriesTransientFetchFailuresWithinThePollBudget()
  {
    var uploadService = new RecordingImageUploadService
    {
      FetchTransientFailuresBeforeSuccess = 1,
      StateResponses = [new ImageUploadState("image-1", "complete", null, false, false)],
    };
    var viewModel = NewViewModel(imageUploadService: uploadService, imageUploadPollInterval: TimeSpan.Zero, imageUploadPollAttempts: 2);

    using var content = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    Assert.True(await viewModel.UploadImageAsync(content, "image/png", content.Length, cancellationToken: TestContext.Current.CancellationToken));

    Assert.Equal(2, uploadService.FetchStateCount);
    Assert.Equal(1, uploadService.CompleteCount);
    var image = Assert.Single(viewModel.Images);
    Assert.True(image.IsReady);
    Assert.Null(image.UploadError);
  }

  [Fact]
  public async Task UploadImageAsyncMarksFailedUploadsAndExcludesThemFromPublish()
  {
    var uploadService = new RecordingImageUploadService { FailUpload = true };
    var postsService = new RecordingPostsService();
    var viewModel = NewViewModel(postsService, uploadService);

    using var content = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    Assert.False(await viewModel.UploadImageAsync(content, "image/png", content.Length, cancellationToken: TestContext.Current.CancellationToken));

    var image = Assert.Single(viewModel.Images);
    Assert.False(image.IsUploading);
    Assert.NotNull(image.UploadError);

    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.TurnstileToken = "token";

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Empty(postsService.Body?.Images ?? []);
  }

  [Fact]
  public async Task UploadImageAsyncSurfacesCreateUploadUrlFailure()
  {
    var uploadService = new RecordingImageUploadService { FailCreateUploadUrl = true };
    var viewModel = NewViewModel(imageUploadService: uploadService);

    using var content = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    Assert.False(await viewModel.UploadImageAsync(content, "image/png", content.Length, cancellationToken: TestContext.Current.CancellationToken));

    Assert.Empty(viewModel.Images);
    Assert.Equal("Image upload failed.", viewModel.ErrorMessage);
    Assert.True(viewModel.HasError);
  }

  [Fact]
  public async Task UploadImageAsyncRejectsOversizedImagesBeforeRequestingUploadUrl()
  {
    var uploadService = new RecordingImageUploadService();
    var viewModel = NewViewModel(imageUploadService: uploadService);

    using var content = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    Assert.False(await viewModel.UploadImageAsync(content, "image/png", MaxImageUploadBytes + 1, cancellationToken: TestContext.Current.CancellationToken));

    Assert.Empty(viewModel.Images);
    Assert.Null(uploadService.ContentType);
    Assert.Equal(0, uploadService.CreateUploadUrlCount);
    Assert.Equal("Image is too large. Maximum size is 50MB.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task UploadImageAsyncRejectsConcurrentCallsWhileTheFirstRequestIsInFlight()
  {
    var uploadService = new RecordingImageUploadService { BlockCreateUploadUrl = true };
    var viewModel = NewViewModel(imageUploadService: uploadService);

    viewModel.ReportImageUploadFailure("Previous upload failed.");

    using var firstContent = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    var firstUpload = viewModel.UploadImageAsync(
        firstContent,
        "image/png",
        firstContent.Length,
        cancellationToken: TestContext.Current.CancellationToken);
    await uploadService.CreateUploadUrlStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsUploadingImages);
    Assert.False(viewModel.CanAddImages);
    Assert.False(viewModel.HasError);
    Assert.Null(viewModel.ErrorMessage);
    viewModel.ResetDraft();

    using var secondContent = new MemoryStream(Encoding.UTF8.GetBytes("png-2"));
    Assert.False(await viewModel.UploadImageAsync(
        secondContent,
        "image/png",
        secondContent.Length,
        cancellationToken: TestContext.Current.CancellationToken));

    uploadService.ReleaseCreateUploadUrl.TrySetResult(true);

    Assert.False(await firstUpload);
    Assert.False(viewModel.IsUploadingImages);
    Assert.True(viewModel.CanAddImages);
    Assert.False(viewModel.HasError);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Empty(viewModel.Images);
  }

  [Fact]
  public async Task UploadImageAsyncClearsStaleGlobalErrorAfterSuccessfulRetry()
  {
    var uploadService = new RecordingImageUploadService();
    var viewModel = NewViewModel(imageUploadService: uploadService);
    viewModel.ReportImageUploadFailure("Unsupported image format.");

    using var content = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    Assert.True(await viewModel.UploadImageAsync(content, "image/png", content.Length, cancellationToken: TestContext.Current.CancellationToken));

    Assert.False(viewModel.HasError);
    Assert.Null(viewModel.ErrorMessage);
    Assert.True(Assert.Single(viewModel.Images).IsReady);
  }

  [Fact]
  public async Task UploadImageAsyncMarksTimeoutOnImageWithoutConstructingReadyState()
  {
    var uploadService = new RecordingImageUploadService
    {
      StateResponses = [new ImageUploadState("image-1", "processing", null, false, false)],
    };
    var viewModel = NewViewModel(
        imageUploadService: uploadService,
        imageUploadPollInterval: TimeSpan.Zero,
        imageUploadPollAttempts: 1);

    using var content = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    Assert.False(await viewModel.UploadImageAsync(content, "image/png", content.Length, cancellationToken: TestContext.Current.CancellationToken));

    var image = Assert.Single(viewModel.Images);
    Assert.False(image.IsReady);
    Assert.Equal("Image upload timed out.", image.UploadError);
    Assert.Equal(1, uploadService.FetchStateCount);
  }

  [Fact]
  public async Task UploadImageAsyncUsesCompletedImageIdAndSkipsDuplicates()
  {
    var uploadService = new RecordingImageUploadService
    {
      CompletedImageId = "existing-image",
      StateResponses = [new ImageUploadState("existing-image", "complete", null, false, false)],
    };
    var viewModel = NewViewModel(imageUploadService: uploadService);

    using var content = new MemoryStream(Encoding.UTF8.GetBytes("duplicate-png"));
    Assert.True(await viewModel.UploadImageAsync(content, "image/png", content.Length, cancellationToken: TestContext.Current.CancellationToken));

    var image = Assert.Single(viewModel.Images);
    Assert.Equal("existing-image", image.ImageId);
    Assert.Equal(0, image.OrderIndex);
    Assert.Equal(1, uploadService.FetchStateCount);

    var duplicateUploadService = new RecordingImageUploadService
    {
      CompletedImageId = "existing-image",
      StateResponses = [new ImageUploadState("existing-image", "complete", null, false, false)],
    };
    var duplicateViewModel = NewViewModel(imageUploadService: duplicateUploadService);
    Assert.True(duplicateViewModel.TryAddImageDraft(new PostComposeImageDraft(
        "existing-image",
        0,
        "Existing",
        UploadError: "Previous upload timed out.")));

    using var duplicateContent = new MemoryStream(Encoding.UTF8.GetBytes("duplicate-png"));
    Assert.True(await duplicateViewModel.UploadImageAsync(duplicateContent, "image/png", duplicateContent.Length, cancellationToken: TestContext.Current.CancellationToken));

    image = Assert.Single(duplicateViewModel.Images);
    Assert.Equal("existing-image", image.ImageId);
    Assert.Equal("Existing", image.Caption);
    Assert.True(image.IsReady);
    Assert.Null(image.UploadError);
    Assert.Equal(1, duplicateUploadService.FetchStateCount);
  }

  [Fact]
  public void ImageCaptionUpdatesClampToThousandCharacters()
  {
    var viewModel = NewViewModel();
    var longCaption = new string('x', 1001);

    viewModel.ImageCaption = longCaption;
    Assert.Equal(1000, viewModel.ImageCaption.Length);

    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("image-1", 0, longCaption)));
    var image = Assert.Single(viewModel.Images);
    Assert.Equal(1000, image.Caption!.Length);

    viewModel.SetImageCaption("image-1", longCaption + "y");
    Assert.Equal(1000, Assert.Single(viewModel.Images).Caption!.Length);
  }

  [Fact]
  public async Task PublishAsyncRejectsActiveImageUploads()
  {
    var postsService = new RecordingPostsService();
    var viewModel = NewViewModel(postsService, new RecordingImageUploadService());
    viewModel.SetImages([new PostComposeImageDraft("image-1", 0, IsUploading: true)]);
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.TurnstileToken = "token";

    Assert.True(viewModel.IsUploadingImages);
    Assert.False(viewModel.Validation.CanPublish);
    Assert.Equal("Image upload is still in progress.", viewModel.Validation.Message);

    Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Null(postsService.Body);
    Assert.Equal("Image upload is still in progress.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task PublishAsyncRejectsImageUploadBatchBeforeIndividualUploadsStart()
  {
    var postsService = new RecordingPostsService();
    var viewModel = NewViewModel(postsService, new RecordingImageUploadService());
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.TurnstileToken = "token";

    using (viewModel.BeginImageUploadBatch())
    {
      Assert.True(viewModel.IsUploadingImages);
      Assert.False(viewModel.CanAddImages);
      Assert.False(viewModel.Validation.CanPublish);
      Assert.Equal("Image upload is still in progress.", viewModel.Validation.Message);
      Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    }

    Assert.False(viewModel.IsUploadingImages);
    Assert.True(viewModel.CanAddImages);
    Assert.True(viewModel.Validation.CanPublish);
    Assert.Null(postsService.Body);
  }

  [Fact]
  public async Task UploadImageAsyncMarksBlockedImagesAndExcludesThemFromPublish()
  {
    var uploadService = new RecordingImageUploadService
    {
      StateResponses = [new ImageUploadState("image-1", "complete", null, false, true)],
    };
    var postsService = new RecordingPostsService();
    var viewModel = NewViewModel(postsService, uploadService);

    using var content = new MemoryStream(Encoding.UTF8.GetBytes("png"));
    Assert.False(await viewModel.UploadImageAsync(content, "image/png", content.Length, cancellationToken: TestContext.Current.CancellationToken));

    var image = Assert.Single(viewModel.Images);
    Assert.False(image.IsUploading);
    Assert.Equal("This image was blocked.", image.UploadError);
    Assert.Equal(1, uploadService.FetchStateCount);

    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.TurnstileToken = "token";

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Empty(postsService.Body?.Images ?? []);
  }

  [Fact]
  public async Task PublishAsyncStillIncludesLegacyManualImageFallback()
  {
    var postsService = new RecordingPostsService();
    var viewModel = NewViewModel(postsService);
    viewModel.Title = "Title";
    viewModel.Markdown = "Body";
    viewModel.TurnstileToken = "token";
    viewModel.ImageId = "legacy-image";
    viewModel.ImageCaption = "Legacy caption";

    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    var image = Assert.Single(postsService.Body?.Images ?? []);
    Assert.Equal("legacy-image", image.ImageId);
    Assert.Equal("Legacy caption", image.Caption);
  }

  private static PostComposeViewModel NewViewModel(
      RecordingPostsService? postsService = null,
      IImageUploadService? imageUploadService = null,
      TimeSpan? imageUploadPollInterval = null,
      int imageUploadPollAttempts = 30) =>
      new(
          postsService ?? new RecordingPostsService(),
          new AppConfig(new Uri("https://api.example.test"), "site-key", true),
          imageUploadService: imageUploadService,
          imageUploadPollInterval: imageUploadPollInterval,
          imageUploadPollAttempts: imageUploadPollAttempts);

  private sealed class RecordingPostsService : IPostsService
  {
    public CreatePostBody? Body { get; private set; }

    public Task<PostsFeedResponse> FetchFeedAsync(
        FetchPostsFeedRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostsFeedResponse> FetchPostsAsync(
        FetchPostsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostResponse> FetchPostAsync(
        string postIdOrSlug,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task VotePostAsync(string postId, int score, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> CreatePostAsync(
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
      Body = body;
      return Task.FromResult(new PostMutationResponse(new Post("post-1", "discussion", "Title", "Body", "user-1")));
    }

    public Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        CreatePostAsync(body, idempotencyKey, cancellationToken);

    public Task<PostMutationResponse> UpdatePostAsync(
        string postIdOrSlug,
        UpdatePostBody body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> ArchivePostAsync(
        string postIdOrSlug,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> UnarchivePostAsync(
        string postIdOrSlug,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeletePostAsync(string postIdOrSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }
}
