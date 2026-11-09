using System.Net;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Tests.Api;
using Voucha.Client.Core.Tests.LandingPages;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task PlacementLimitPreservesProfileAndAllowsRetry(bool replaceImage)
  {
    var settings = new RecordingSettingsService
    {
      IdentityFailure = new VouchaApiException(HttpStatusCode.Conflict,
          ApiFixtureLoader.LoadResponse("native.posts.images.placement-limit.conflict")),
    };
    var viewModel = new ProfileViewModel(settings, new RecordingLandingPagesService(),
        new RecordingPostsService(), replaceImage ? new SequentialAvatarImageService() : new RecordingImageUploadService(),
        new AppConfig(new Uri("https://api.example.test"), "site-key"));
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);
    var previousUser = viewModel.User;
    var previousIdentity = viewModel.Identity;
    await using var content = new MemoryStream([1, 2, 3]);
    Assert.False(await viewModel.UploadAvatarAsync(content, "image/png", content.Length, TestContext.Current.CancellationToken));
    Assert.Equal("This image has reached the placement limit. Choose another image or remove an existing use and retry.", viewModel.ErrorMessage);
    Assert.Equal(previousUser, viewModel.User);
    Assert.Equal(previousIdentity, viewModel.Identity);
    Assert.Equal("Hello", viewModel.BioMarkdown);
    Assert.True(viewModel.CanMutateAvatar);

    settings.IdentityFailure = null;
    content.Position = 0;
    Assert.True(await viewModel.UploadAvatarAsync(content, "image/png", content.Length, TestContext.Current.CancellationToken));
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(replaceImage ? "image-2" : "image-1", viewModel.ProfileImageId);
  }
}
