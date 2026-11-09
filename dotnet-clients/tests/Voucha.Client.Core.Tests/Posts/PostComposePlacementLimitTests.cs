using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed partial class PostComposeImageTests
{
  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public async Task PlacementLimitPreservesDraftAndAllowsRetry(bool community, bool replaceImage)
  {
    var service = new RecordingPostsService
    {
      Failure = new VouchaApiException(HttpStatusCode.Conflict,
          ApiFixtureLoader.LoadResponse("native.posts.images.placement-limit.conflict")),
    };
    var viewModel = NewViewModel(service);
    viewModel.Title = "Draft title";
    viewModel.Markdown = "Draft body";
    viewModel.CommunitySlug = community ? "community" : "";
    viewModel.TurnstileToken = "token";
    viewModel.SetImages([new PostComposeImageDraft("limited-image", 0, "Caption")]);

    Assert.False(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Equal("This image has reached the placement limit. Choose another image or remove an existing use and retry.", viewModel.ErrorMessage);
    Assert.Equal("Draft title", viewModel.Title);
    Assert.Equal("Draft body", viewModel.Markdown);
    Assert.Equal("limited-image", Assert.Single(viewModel.Images).ImageId);
    Assert.Equal("Caption", Assert.Single(viewModel.Images).Caption);
    Assert.False(viewModel.HasPublished);

    service.Failure = null; // Capacity can also be freed without changing the selection.
    if (replaceImage) viewModel.SetImages([new PostComposeImageDraft("replacement", 0, "Caption")]);
    viewModel.TurnstileToken = "new-token";
    Assert.True(await viewModel.PublishAsync(TestContext.Current.CancellationToken));
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(replaceImage ? "replacement" : "limited-image", Assert.Single(service.Body!.Images!).ImageId);
  }

}
