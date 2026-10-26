using System.Xml.Linq;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class ProfilePageVotingWiringTests
{
  [Fact]
  public void ProfileHistoryRendersProvenanceOnlyWhenThePostHasIt()
  {
    var markup = XDocument.Parse(AppSource.Read("Pages", "ProfilePage.xaml"));
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
    var page = AppSource.Read("Pages", "ProfilePage.xaml.cs");
    var voting = AppSource.Read("Pages", "ProfilePage.Voting.cs");

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
    var markup = AppSource.Read("Pages", "ProfilePage.xaml");

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
    var page = AppSource.Read("Pages", "ProfilePage.xaml.cs");
    var markup = AppSource.Read("Pages", "ProfilePage.xaml");

    Assert.Contains("CanCreateEntityRelationVote(isUserTag: true)", page, StringComparison.Ordinal);
    Assert.Contains("<pages:VoteClearEligibilityConverter />", markup, StringComparison.Ordinal);
    Assert.Contains("<Binding Path=\"MyVote\" />", markup, StringComparison.Ordinal);
    Assert.Contains("Path=\"BindingContext.CanClearUserTagVotes\"", markup, StringComparison.Ordinal);
  }

  private static int Count(string source, string value) =>
      source.Split(value, StringSplitOptions.None).Length - 1;
}
