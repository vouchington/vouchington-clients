using System.Runtime.CompilerServices;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Moderation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class ReviewQueuePageSourceTests
{
  [Fact]
  public void ReviewQueueUsesDedicatedNativePageAndTypedActions()
  {
    var xaml = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Pages", "ReviewQueuePage.xaml"));
    var routing = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "AppShell.ModerationRoutes.cs"));
    var pageCode = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.App", "Pages", "ReviewQueuePage.xaml.cs"));
    var genericViewModel = File.ReadAllText(RepoPath("dotnet-clients", "src", "Voucha.Client.Core", "Moderation", "ModerationViewModel.cs"));

    Assert.Contains("x:DataType=\"moderation:ReviewQueueViewModel\"", xaml, StringComparison.Ordinal);
    Assert.Contains("OnApproveClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("OnRejectClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("OnReReviewClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("OnRevealClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("OnCheckExposureClicked", xaml, StringComparison.Ordinal);
    Assert.Contains("CreatedPresentation", xaml, StringComparison.Ordinal);
    Assert.Contains("DispositionPresentation", xaml, StringComparison.Ordinal);
    Assert.Contains("FlaggedCategoriesPresentation", xaml, StringComparison.Ordinal);
    Assert.Contains("SignalsPresentation", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("SpamPresentation", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("OpenAIModerationPresentation", xaml, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding IsLoading}\"", xaml, StringComparison.Ordinal);
    Assert.Contains("IsVisible=\"{Binding ShowEmptyState}\"", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("<RefreshView IsRefreshing=\"{Binding IsRefreshing}\" IsEnabled=", xaml, StringComparison.Ordinal);
    Assert.Contains("<controls:HybridPaginationControl", xaml, StringComparison.Ordinal);
    Assert.Contains("PaginationId=\"review-queue\"", xaml, StringComparison.Ordinal);
    Assert.Contains("HasMore=\"{Binding HasMore}\"", xaml, StringComparison.Ordinal);
    Assert.DoesNotContain("CollectionView.EmptyView", xaml, StringComparison.Ordinal);
    Assert.Contains("GetRequiredService<ReviewQueuePage>()", routing, StringComparison.Ordinal);
    Assert.Contains("foreach (var content in EnumerateShellContents(\"moderation\"))", routing, StringComparison.Ordinal);
    Assert.Contains("content.Content as ReviewQueuePage", routing, StringComparison.Ordinal);
    Assert.Contains(
        "ModerationDisputesPageLifetime.Replace(content, reviewQueuePage)",
        routing,
        StringComparison.Ordinal);
    Assert.Contains("await reviewQueuePage.ReloadAsync()", routing, StringComparison.Ordinal);
    Assert.Contains(
        "ModerationDisputesPageLifetime.Replace(content, moderationPage)",
        routing,
        StringComparison.Ordinal);
    Assert.Contains("CancellationTokenSource? pageCancellation", pageCode, StringComparison.Ordinal);
    Assert.Contains("pageCancellation?.Cancel()", pageCode, StringComparison.Ordinal);
    Assert.Contains("pageCancellation?.Dispose()", pageCode, StringComparison.Ordinal);
    Assert.Contains("viewModel.CancelListOperations()", pageCode, StringComparison.Ordinal);
    Assert.Contains("public Task ReloadAsync() => viewModel.ReloadAsync(CurrentPageToken())", pageCode, StringComparison.Ordinal);
    Assert.Contains("await RunPageOperationAsync(viewModel.ResumeAsync)", pageCode, StringComparison.Ordinal);
    Assert.Contains("await RunPageOperationAsync(viewModel.RefreshAsync)", pageCode, StringComparison.Ordinal);
    Assert.Contains("if (sender is RefreshView refreshView) refreshView.IsRefreshing = false;", pageCode, StringComparison.Ordinal);
    Assert.Contains("await RunPageOperationAsync(viewModel.LoadMoreAsync)", pageCode, StringComparison.Ordinal);
    var asyncVoidBoundaryCount = pageCode.Split("async void ", StringSplitOptions.None).Length - 1;
    var safeOperationCount = pageCode.Split("await RunPageOperationAsync(", StringSplitOptions.None).Length - 1;
    Assert.Equal(8, asyncVoidBoundaryCount);
    Assert.Equal(asyncVoidBoundaryCount, safeOperationCount);
    Assert.Contains("CA1031:Do not catch general exception types", pageCode, StringComparison.Ordinal);
    Assert.Contains("catch (Exception ex)", pageCode, StringComparison.Ordinal);
    Assert.Contains("Debug.WriteLine(ex)", pageCode, StringComparison.Ordinal);
    Assert.DoesNotContain("LoadReviewQueueAsync", genericViewModel, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("/posts/review-queue", ModerationRouteKind.ReviewQueue)]
  [InlineData("/reports", ModerationRouteKind.Reports)]
  public void QueueAndGenericModerationRoutesResolveToDifferentPageContexts(string path, ModerationRouteKind expectedKind)
  {
    Assert.True(ModerationRoutes.TryResolve(path, out var context));
    Assert.Equal(expectedKind, context.RouteKind);
  }

  [Theory]
  [InlineData(false, null, false, true)]
  [InlineData(true, "member", false, false)]
  [InlineData(true, "administrator", true, false)]
  public void ReviewQueueDeepLinkIsAdministratorOnly(bool authenticated, string? role, bool canNavigate, bool queuesSignIn)
  {
    var roles = role is null ? Array.Empty<string>() : new[] { role };
    var resolution = NativeDeepLinkResolver.Resolve("voucha://posts/review-queue", new NavigationViewer(authenticated, roles));

    Assert.Equal(NativeRouteDestinationId.ModerationReviewQueue, resolution.DestinationId);
    Assert.Equal(canNavigate, resolution.CanNavigate);
    Assert.Equal(queuesSignIn, resolution.ShouldQueueUntilAuthenticated);
  }

  private static string RepoPath(params string[] segments) =>
      RepoPathFromSource(segments);

  private static string RepoPathFromSource(
      string[] segments,
      [CallerFilePath] string sourcePath = "")
  {
    var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath)!);
    while (directory is not null)
    {
      var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
      if (File.Exists(candidate)) return candidate;
      directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}
