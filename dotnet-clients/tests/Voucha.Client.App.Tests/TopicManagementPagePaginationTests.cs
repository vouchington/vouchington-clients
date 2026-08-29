using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class TopicManagementPagePaginationTests
{
  [Fact]
  public void AliasesAndAdditionalHostnamesWireHybridPaginationControls()
  {
    var markup = AppSource("Pages", "TopicManagementPage.xaml");
    var constructorSource = AppSource("Pages", "TopicManagementPage.xaml.cs");
    var paginationSource = AppSource("Pages", "TopicManagementPage.Pagination.cs");
    var operationsSource = AppSource("Pages", "TopicManagementPage.Operations.cs");

    Assert.Contains("x:Name=\"AliasesPaginationControl\"", markup, StringComparison.Ordinal);
    Assert.Contains("PaginationId=\"topic-aliases\"", markup, StringComparison.Ordinal);
    Assert.Contains("LoadNextPageRequested=\"OnLoadMoreAliasesRequested\"", markup, StringComparison.Ordinal);
    Assert.Contains("x:Name=\"HostnamesPaginationControl\"", markup, StringComparison.Ordinal);
    Assert.Contains("PaginationId=\"topic-additional-hostnames\"", markup, StringComparison.Ordinal);
    Assert.Contains("LoadNextPageRequested=\"OnLoadMoreHostnamesRequested\"", markup, StringComparison.Ordinal);

    Assert.Contains("SyncAliasesPaginationControl();", constructorSource, StringComparison.Ordinal);
    Assert.Contains("SyncHostnamesPaginationControl();", constructorSource, StringComparison.Ordinal);

    Assert.Contains("CursorPaginationState<TopicAlias, string> aliasesPagination = new(alias => alias.Id);", paginationSource, StringComparison.Ordinal);
    Assert.Contains("CursorPaginationState<TopicAdditionalHostname, string> hostnamesPagination = new(host => host.HostnameId);", paginationSource, StringComparison.Ordinal);
    Assert.Contains("private async void OnLoadMoreAliasesRequested(object? sender, EventArgs e)", paginationSource, StringComparison.Ordinal);
    Assert.Contains("private async void OnLoadMoreHostnamesRequested(object? sender, EventArgs e)", paginationSource, StringComparison.Ordinal);
    Assert.Contains("aliasesPagination.BeginNextPage()", paginationSource, StringComparison.Ordinal);
    Assert.Contains("hostnamesPagination.BeginNextPage()", paginationSource, StringComparison.Ordinal);
    Assert.Contains(
        "aliasesPagination.Complete(request, response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage)",
        paginationSource,
        StringComparison.Ordinal);
    Assert.Contains(
        "hostnamesPagination.Complete(request, response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage)",
        paginationSource,
        StringComparison.Ordinal);
    Assert.Contains("aliasesPagination.Cancel(request);", paginationSource, StringComparison.Ordinal);
    Assert.Contains("hostnamesPagination.Cancel(request);", paginationSource, StringComparison.Ordinal);
    Assert.Contains("aliasesPagination.Fail(request, ex.Message);", paginationSource, StringComparison.Ordinal);
    Assert.Contains("hostnamesPagination.Fail(request, ex.Message);", paginationSource, StringComparison.Ordinal);

    Assert.Contains("aliasesPagination.Reset(response.Results);", operationsSource, StringComparison.Ordinal);
    Assert.Contains(
        "aliasesPagination.RestoreContinuation(response.PageInfo.EndCursor, response.PageInfo.HasNextPage);",
        operationsSource,
        StringComparison.Ordinal);
    Assert.Contains("hostnamesPagination.Reset(response.Results);", operationsSource, StringComparison.Ordinal);
    Assert.Contains(
        "hostnamesPagination.RestoreContinuation(response.PageInfo.EndCursor, response.PageInfo.HasNextPage);",
        operationsSource,
        StringComparison.Ordinal);
  }

  private static string AppSource(string directory, string file, [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return File.ReadAllText(Path.Combine(
        root?.FullName ?? throw new DirectoryNotFoundException(),
        "src", "Voucha.Client.App", directory, file));
  }
}
