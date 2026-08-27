using Xunit;
using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class ImportExportRouteTests
{
  [Theory]
  [InlineData("/my/topics/import-export", "topics")]
  [InlineData("/my/sources/import-export", "web-search")]
  [InlineData("/my/news-sources/import-export", "news")]
  [InlineData("/my/podcasts/import-export", "podcasts")]
  [InlineData("/my/channels/import-export", "videos")]
  public void ImportExportRoutesUseOneAuthenticatedNativeDestination(string path, string intentId)
  {
    var anonymous = NativeDeepLinkResolver.Resolve(path, NavigationViewer.Anonymous);
    var signedIn = NativeDeepLinkResolver.Resolve(path, new NavigationViewer(true, []));

    Assert.Equal(NativeRouteDestinationId.ImportExport, anonymous.DestinationId);
    Assert.True(anonymous.ShouldQueueUntilAuthenticated);
    Assert.Equal(NativeRouteDestinationId.ImportExport, signedIn.DestinationId);
    Assert.Equal(intentId, signedIn.IntentId);
    Assert.True(signedIn.CanNavigate);
  }

  [Theory]
  [InlineData("/my/topics/import-export", ImportExportOwner.Topics, null)]
  [InlineData("/my/sources/import-export", ImportExportOwner.Sources, null)]
  [InlineData("/my/news-sources/import-export", ImportExportOwner.Sources, "article")]
  [InlineData("/my/podcasts/import-export", ImportExportOwner.Sources, "podcast")]
  [InlineData("/my/channels/import-export", ImportExportOwner.Sources, "video")]
  public void RouteContextDerivesOwnerAndInitialExportFilter(
      string path,
      ImportExportOwner owner,
      string? feedType)
  {
    var context = ImportExportRouteContext.FromPath(path);

    Assert.Equal(owner, context.Owner);
    Assert.Equal(feedType, context.InitialFeedType);
  }
}
