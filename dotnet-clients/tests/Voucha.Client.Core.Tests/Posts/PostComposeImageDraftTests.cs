using System.Text.Json;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed class PostComposeImageDraftTests
{
  [Fact]
  public void DisplayLabelUsesOneBasedOrderIndex()
  {
    Assert.Equal("Image 1", new PostComposeImageDraft("image-1", 0).DisplayLabel);
    Assert.Equal("Image 5", new PostComposeImageDraft("image-2", 4).DisplayLabel);
  }

  [Fact]
  public void TryAddImageDraftNormalizesOrderIndexesAndLabels()
  {
    var viewModel = NewViewModel();

    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("image-2", 9, "Second")));
    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("image-1", 3, "First")));

    Assert.Collection(
        viewModel.Images,
        image =>
        {
          Assert.Equal("image-2", image.ImageId);
          Assert.Equal(0, image.OrderIndex);
          Assert.Equal("Image 1", image.DisplayLabel);
        },
        image =>
        {
          Assert.Equal("image-1", image.ImageId);
          Assert.Equal(1, image.OrderIndex);
          Assert.Equal("Image 2", image.DisplayLabel);
        });
  }

  [Fact]
  public void MoveImageRenormalizesOrderIndexesAndLabels()
  {
    var viewModel = NewViewModel();

    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("image-1", 0)));
    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("image-2", 1)));
    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("image-3", 2)));

    viewModel.MoveImage(2, 0);

    Assert.Collection(
        viewModel.Images,
        image =>
        {
          Assert.Equal("image-3", image.ImageId);
          Assert.Equal(0, image.OrderIndex);
          Assert.Equal("Image 1", image.DisplayLabel);
        },
        image =>
        {
          Assert.Equal("image-1", image.ImageId);
          Assert.Equal(1, image.OrderIndex);
          Assert.Equal("Image 2", image.DisplayLabel);
        },
        image =>
        {
          Assert.Equal("image-2", image.ImageId);
          Assert.Equal(2, image.OrderIndex);
          Assert.Equal("Image 3", image.DisplayLabel);
        });
  }

  [Fact]
  public void RemoveImageRenormalizesOrderIndexesAndLabels()
  {
    var viewModel = NewViewModel();

    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("image-1", 0)));
    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("image-2", 1)));
    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft("image-3", 2)));

    viewModel.RemoveImage("image-2");

    Assert.Collection(
        viewModel.Images,
        image =>
        {
          Assert.Equal("image-1", image.ImageId);
          Assert.Equal(0, image.OrderIndex);
          Assert.Equal("Image 1", image.DisplayLabel);
        },
        image =>
        {
          Assert.Equal("image-3", image.ImageId);
          Assert.Equal(1, image.OrderIndex);
          Assert.Equal("Image 2", image.DisplayLabel);
        });
  }

  [Fact]
  public void LocalSelectedBytesAreTransientAndRemovedWithDraft()
  {
    var viewModel = NewViewModel();
    var bytes = new byte[] { 1, 2, 3 };
    Assert.True(viewModel.TryAddImageDraft(new PostComposeImageDraft(
        "image-1", 0, LocalPreviewBytes: bytes)));

    Assert.True(Assert.Single(viewModel.Images).HasLocalPreview);
    Assert.DoesNotContain(
        "LocalPreviewBytes",
        JsonSerializer.Serialize(new PostComposeImageDraft("image-1", 0, LocalPreviewBytes: bytes)),
        StringComparison.Ordinal);

    viewModel.EndLocalImagePreviewSession();
    Assert.False(Assert.Single(viewModel.Images).HasLocalPreview);
    viewModel.RemoveImage("image-1");
    Assert.Empty(viewModel.Images);
  }

  private static PostComposeViewModel NewViewModel() =>
      new(new NoopPostsService(), new AppConfig(new Uri("https://api.example.test"), "site-key", true));

  private sealed class NoopPostsService : IPostsService
  {
    public Task<PostsFeedResponse> FetchFeedAsync(
        FetchPostsFeedRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostsFeedResponse> FetchPostsAsync(
        FetchPostsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> CreatePostAsync(
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PostMutationResponse> CreateCommunityPostAsync(
        string communityIdOrSlug,
        CreatePostBody body,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

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

    public Task VotePostAsync(
        string postId,
        int score,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeletePostAsync(
        string postIdOrSlug,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }
}
