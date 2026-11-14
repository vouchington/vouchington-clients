using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelActionsTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task IdentityImageConflictPreservesInputsAndCanRetry(bool replaceImage)
  {
    var service = new RecordingSettingsService
    {
      UpdateIdentityOverride = _ => Task.FromException<MyIdentityResponse>(new VouchaApiException(
          HttpStatusCode.Conflict, ApiFixtureLoader.LoadResponse("native.posts.images.placement-limit.conflict"))),
    };
    using var viewModel = new SettingsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.Username = "draft-user";
    viewModel.ProfileImageId = "limited-image";
    await viewModel.SaveIdentityAsync(TestContext.Current.CancellationToken);

    Assert.Equal("This image has reached the placement limit. Choose another image or remove an existing use and retry.", viewModel.ErrorMessage);
    Assert.Equal("draft-user", viewModel.Username);
    Assert.Equal("limited-image", viewModel.ProfileImageId);
    UpdateMyIdentityBody? retried = null;
    service.UpdateIdentityOverride = body =>
    {
      retried = body;
      return Task.FromResult(RecordingSettingsService.CreateIdentity());
    };
    if (replaceImage) viewModel.ProfileImageId = "replacement";
    await viewModel.SaveIdentityAsync(TestContext.Current.CancellationToken);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal("draft-user", retried!.Username);
    Assert.Equal(replaceImage ? "replacement" : "limited-image", retried.ProfileImageId?.Value);
  }
}
