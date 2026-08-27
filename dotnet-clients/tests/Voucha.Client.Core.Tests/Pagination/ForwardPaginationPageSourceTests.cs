using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.Core.Tests.Pagination;

public sealed class ForwardPaginationPageSourceTests
{
  [Theory]
  [InlineData("ChatListPage.xaml", "ChatListPage.xaml.cs")]
  [InlineData("SupportThreadsPage.xaml", "SupportThreadsPage.xaml.cs")]
  [InlineData("FriendsPage.xaml", "FriendsPage.xaml.cs")]
  [InlineData("NotificationsPage.xaml", "NotificationsPage.xaml.cs")]
  [InlineData("DirectMessagesPage.xaml", "DirectMessagesPage.xaml.cs")]
  [InlineData("ReviewQueuePage.xaml", "ReviewQueuePage.xaml.cs")]
  [InlineData("ReferralLinksPage.xaml", "ReferralLinksPage.xaml.cs")]
  [InlineData("PostsPage.xaml", "PostsPage.xaml.cs")]
  public void XamlForwardListsExposeManualAndAutomaticContinuation(string markupFile, string codeFile)
  {
    var markup = PageSource(markupFile);
    var code = PageSource(codeFile);

    Assert.Contains("HybridPaginationControl", markup, StringComparison.Ordinal);
    Assert.Contains("RemainingItemsThresholdReached", markup, StringComparison.Ordinal);
    Assert.Contains("TryLoadAutomatically", code, StringComparison.Ordinal);
  }

  [Fact]
  public void PaymentCardsScrollableForwardListExposesManualAndAutomaticContinuation()
  {
    var markup = PageSource("PaymentCardsPage.xaml");
    var code = PageSource("PaymentCardsPage.xaml.cs");

    Assert.Contains("HybridPaginationControl", markup, StringComparison.Ordinal);
    Assert.Contains("PaginationId=\"payment-cards\"", markup, StringComparison.Ordinal);
    Assert.Contains("PaginationId=\"payment-card-parent-options\"", markup, StringComparison.Ordinal);
    Assert.Contains("Scrolled=", markup, StringComparison.Ordinal);
    Assert.Contains("TryLoadAutomatically", code, StringComparison.Ordinal);
    Assert.Contains("IsEffectivelyVisible", code, StringComparison.Ordinal);
    Assert.Contains("paginationVisibility.Rearm(ParentCardsPagination)", code, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("AgentListsPage.cs")]
  [InlineData("CommunitySectionPages.cs")]
  [InlineData("ModerationAppealsPage.cs")]
  [InlineData("ModerationReportsPage.cs")]
  public void CodeBuiltForwardListsExposeManualAndAutomaticContinuation(string file)
  {
    var source = PageSource(file);

    Assert.Contains("HybridPaginationControl", source, StringComparison.Ordinal);
    Assert.True(
        source.Contains("RemainingItemsThreshold", StringComparison.Ordinal) ||
        source.Contains(".Scrolled +=", StringComparison.Ordinal));
    Assert.Contains("TryLoadAutomatically", source, StringComparison.Ordinal);
  }

  private static string PageSource(string file) =>
      File.ReadAllText(Path.Combine(RepoRoot(), "dotnet-clients", "src", "Voucha.Client.App", "Pages", file));

  private static string RepoRoot([CallerFilePath] string sourcePath = "")
  {
    DirectoryInfo? directory = new(Path.GetDirectoryName(sourcePath)!);
    while (directory is not null)
    {
      if (File.Exists(Path.Combine(directory.FullName, "api-fixtures", "v1", "manifest.json")))
        return directory.FullName;
      directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find repository root from source path.");
  }
}
