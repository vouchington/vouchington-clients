using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class ProfilePageVotingWiringTests
{
  [Fact]
  public void ProfileHistoryRendersProvenanceOnlyWhenThePostHasIt()
  {
    var markup = XDocument.Parse(AppSource("ProfilePage.xaml"));
    XNamespace maui = "http://schemas.microsoft.com/dotnet/2021/maui";
    var history = Assert.Single(markup.Descendants(maui + "CollectionView"),
        element => (string?)element.Attribute("ItemsSource") == "{Binding HistoryItems}");
    var template = Assert.Single(history.Descendants(maui + "DataTemplate"));
    var provenance = Assert.Single(template.Descendants(maui + "Label"),
        element => (string?)element.Attribute("Text") == "{Binding LocalizedProvenanceLabel}");

    Assert.Equal("{Binding HasProvenance}", (string?)provenance.Attribute("IsVisible"));
  }

  [Fact]
  public void ProfileUserTrustVotesHandOffVerificationRecoveryAfterBothMutations()
  {
    var page = AppSource("ProfilePage.xaml.cs");
    var voting = AppSource("ProfilePage.Voting.cs");

    Assert.Contains(
        "emailRecovery = serviceProvider.GetRequiredService<EmailVerificationRecoveryCoordinator>();",
        page,
        StringComparison.Ordinal);
    Assert.Equal(2, Count(voting, "await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);"));
    Assert.Contains("await viewModel.VoteUserTrustAsync(choice);", voting, StringComparison.Ordinal);
    Assert.Contains("await viewModel.VoteUserTrustAsync(null);", voting, StringComparison.Ordinal);
  }

  [Fact]
  public void ProfileTrustContextLabelsFollowedSignalsInsteadOfGlobalVoteTotals()
  {
    var markup = AppSource("ProfilePage.xaml");

    Assert.Contains(
        "Text=\"{Binding LocalizedPositiveSignalsFromFollowing}\"",
        markup,
        StringComparison.Ordinal);
    Assert.Contains(
        "Text=\"{Binding LocalizedNegativeSignalsFromFollowing}\"",
        markup,
        StringComparison.Ordinal);
    Assert.DoesNotContain("Text=\"{Binding VoteCountUp}\" Style=\"{StaticResource Metadata}\"", markup, StringComparison.Ordinal);
    Assert.DoesNotContain("Text=\"{Binding VoteCountDown}\" Style=\"{StaticResource Metadata}\"", markup, StringComparison.Ordinal);
  }

  [Fact]
  public void ProfileUserTagControlsUseRelationPolicyAndOnlyEnableClearForRowsWithVotes()
  {
    var page = AppSource("ProfilePage.xaml.cs");
    var markup = AppSource("ProfilePage.xaml");

    Assert.Contains("CanCreateEntityRelationVote(isUserTag: true)", page, StringComparison.Ordinal);
    Assert.Contains("<pages:VoteClearEligibilityConverter />", markup, StringComparison.Ordinal);
    Assert.Contains("<Binding Path=\"MyVote\" />", markup, StringComparison.Ordinal);
    Assert.Contains("Path=\"BindingContext.CanClearUserTagVotes\"", markup, StringComparison.Ordinal);
  }

  private static string AppSource(string file, [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return File.ReadAllText(Path.Combine(
        root?.FullName ?? throw new DirectoryNotFoundException(),
        "src", "Voucha.Client.App", "Pages", file));
  }

  private static int Count(string source, string value) =>
      source.Split(value, StringSplitOptions.None).Length - 1;
}
