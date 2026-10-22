using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class HouseholdRouteSourceTests
{
  [Fact]
  public void MauiShellRoutesHouseholdDirectlyBeforeGenericSettings()
  {
    var appLinks = File.ReadAllText(RepositoryFilePath.Find("dotnet-clients", "src", "Voucha.Client.App", "AppShell.AppLinks.cs"));
    var directRoute = File.ReadAllText(RepositoryFilePath.Find("dotnet-clients", "src", "Voucha.Client.App", "AppShell.HouseholdRoute.cs"));

    var directIndex = appLinks.IndexOf("TryOpenHouseholdRouteAsync", StringComparison.Ordinal);
    var genericIndex = appLinks.IndexOf("OpenIntentAsync(intentId, match: resolution.Match)", StringComparison.Ordinal);
    Assert.True(directIndex >= 0 && directIndex < genericIndex);
    Assert.Contains("NativeRouteDestinationId.Household", directRoute, StringComparison.Ordinal);
    Assert.Contains("GetRequiredService<HouseholdPage>()", directRoute, StringComparison.Ordinal);
  }

  [Fact]
  public void HouseholdPageRendersRequiredLabelsConfirmationAndBindings()
  {
    var xaml = File.ReadAllText(RepositoryFilePath.Find("dotnet-clients", "src", "Voucha.Client.App", "Pages", "HouseholdPage.xaml"));
    var code = File.ReadAllText(RepositoryFilePath.Find("dotnet-clients", "src", "Voucha.Client.App", "Pages", "HouseholdPage.xaml.cs"));

    Assert.Contains("UiMessageKey.NativeSwiftHouseholdsBookmarksHouseholdYouBelongTo", Source("HouseholdSection.cs"), StringComparison.Ordinal);
    Assert.Contains("Text=\"{Binding LocalizedTitle}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("Text=\"{DynamicResource native.swift.householdsBookmarks.readOnly}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("ShowsEmptyState", xaml, StringComparison.Ordinal);
    Assert.Contains("Text=\"{DynamicResource native.swift.householdsBookmarks.createHousehold}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("controls:HybridPaginationControl", xaml, StringComparison.Ordinal);
    Assert.Contains("LoadNextPageRequested=\"OnLoadMoreMembersRequested\"", xaml, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftHouseholdsBookmarksRemoveMemberConfirmation", code, StringComparison.Ordinal);
    Assert.Contains("member.LocalizedDisplayName", code, StringComparison.Ordinal);
    Assert.Contains("Text=\"{Binding LocalizedDisplayName}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("Text=\"{Binding ProtocolRelationship}\"", xaml, StringComparison.Ordinal);
  }

  private static string Source(string file) => File.ReadAllText(RepositoryFilePath.Find(
      "dotnet-clients", "src", "Voucha.Client.Core", "Households", file));
}
