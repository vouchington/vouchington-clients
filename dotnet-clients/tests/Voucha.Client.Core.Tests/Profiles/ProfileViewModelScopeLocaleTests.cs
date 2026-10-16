using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Profiles;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelSafetyTests
{
  [Theory]
  [InlineData(NativeUserProfileCollectionKind.TopicsFollowing)]
  [InlineData(NativeUserProfileCollectionKind.SourcesAll)]
  [InlineData(NativeUserProfileCollectionKind.CommunitiesMember)]
  public async Task VisibleCollectionProvenanceRelocalizesWithoutRefetch(
      NativeUserProfileCollectionKind collection)
  {
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var service = new RecordingProfileCollectionsService { IncludeProvenance = true };
    using var viewModel = NewViewModel(new User("user-2", "bob", "Hello"),
        localization: new UiLocalization(controller), localeController: controller);
    viewModel.ConfigureProfileCollectionsService(service);
    await viewModel.LoadPublicScopeAsync("bob",
        new NativeUserProfileScope(SectionForTest(collection), collection),
        TestContext.Current.CancellationToken);
    viewModel.SetCurrentViewer("staff-1", "staff", isStaffViewer: true);
    var before = Labels(viewModel, collection);
    var requests = service.Calls.Count;

    controller.ApplySavedLocale("es");

    var after = Labels(viewModel, collection);
    Assert.NotEqual(before.Public, after.Public);
    Assert.NotEqual(before.Staff, after.Staff);
    Assert.Equal(requests, service.Calls.Count);
  }

  private static (string? Public, string? Staff) Labels(
      ProfileViewModel model, NativeUserProfileCollectionKind collection) => collection switch
      {
        NativeUserProfileCollectionKind.TopicsFollowing =>
            (Assert.Single(model.TopicItems).LocalizedProvenanceLabel, Assert.Single(model.TopicItems).StaffChannelLabel),
        NativeUserProfileCollectionKind.SourcesAll =>
            (Assert.Single(model.SourceItems).LocalizedProvenanceLabel, Assert.Single(model.SourceItems).StaffChannelLabel),
        NativeUserProfileCollectionKind.CommunitiesMember =>
            (Assert.Single(model.CommunityItems).LocalizedProvenanceLabel, Assert.Single(model.CommunityItems).StaffChannelLabel),
        _ => throw new ArgumentOutOfRangeException(nameof(collection)),
      };

}
