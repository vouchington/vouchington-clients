using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed class ProfilePageSourceTests
{
  [Fact]
  public void ProfilePageBindsScopedTabsCollectionsHeaderActionsAndPagination()
  {
    var xaml = ReadPage("ProfilePage.xaml");
    var scopes = ReadPage("ProfilePage.ProfileScopes.cs");
    var safeRun = ReadPage("ProfilePage.SafeRun.cs");

    Assert.Contains("ItemsSource=\"{Binding PrimaryTabs}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("ItemsSource=\"{Binding ContextualTabs}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("ItemsSource=\"{Binding FriendItems}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("ItemsSource=\"{Binding TopicItems}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("ItemsSource=\"{Binding SourceItems}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("ItemsSource=\"{Binding CommunityItems}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding CanFollowUser}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding CanUnfollowUser}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("Clicked=\"OnLoadMoreClicked\"", xaml, StringComparison.Ordinal);
    Assert.Contains("RunProfileActionAsync(() => viewModel.SelectScopeAsync(row.Collection))", scopes, StringComparison.Ordinal);
    Assert.Contains("RunProfileActionAsync(() => viewModel.LoadMoreAsync())", scopes, StringComparison.Ordinal);
    Assert.Contains("catch (Exception ex)", safeRun, StringComparison.Ordinal);
    Assert.Contains("viewModel.ReportUnexpectedProfileActionError(ex);", safeRun, StringComparison.Ordinal);
    Assert.Contains("ProfileNavigationTargets.SignIn", scopes, StringComparison.Ordinal);
    Assert.Contains("ProfileNavigationTargets.Topic(row)", scopes, StringComparison.Ordinal);
    Assert.Contains("ProfileNavigationTargets.Source(row)", scopes, StringComparison.Ordinal);
    Assert.Contains("ProfileNavigationTargets.Community(row)", scopes, StringComparison.Ordinal);
    Assert.Contains("Text=\"{DynamicResource native.dotnet.profile.selected}\" IsVisible=\"{Binding IsSelected}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("ItemsSource=\"{Binding HistoryTabs}\" Direction=\"Row\" Wrap=\"Wrap\" IsVisible=\"{Binding IsOverviewScope}\"", xaml, StringComparison.Ordinal);
  }

  private static string ReadPage(string file, [CallerFilePath] string sourceFile = "") => File.ReadAllText(RepoPath(
      sourceFile,
      "dotnet-clients", "src", "Voucha.Client.App", "Pages", file));

  private static string RepoPath(string sourceFile, params string[] parts)
  {
    foreach (var start in new[] { Path.GetDirectoryName(sourceFile)!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
      var directory = new DirectoryInfo(start);
      while (directory is not null)
      {
        var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
        if (File.Exists(candidate)) return candidate;
        directory = directory.Parent;
      }
    }
    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}
