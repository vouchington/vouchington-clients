using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class TopicRecommendationDetailPageSourceTests
{
  [Fact]
  public void VoteActionsBindSessionPermissionsAndInFlightState()
  {
    var source = AppSource("TopicRecommendationDetailPage.cs");

    Assert.Contains("sessionStore.Current.CanCastPublicVotes() && viewModel.CanCastVote", source, StringComparison.Ordinal);
    Assert.Contains("sessionStore.Current.CanClearPublicVote(viewModel.CurrentVoteChoice) &&", source, StringComparison.Ordinal);
    Assert.Contains("viewModel.CanClearVote", source, StringComparison.Ordinal);
    Assert.Contains("nameof(TopicRecommendationDetailViewModel.IsVoting)", source, StringComparison.Ordinal);
    Assert.Contains("if (!vote.IsEnabled) return;", source, StringComparison.Ordinal);
    Assert.Contains("if (!clearVote.IsEnabled) return;", source, StringComparison.Ordinal);
  }

  private static string AppSource(string file, [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return File.ReadAllText(Path.Combine(
        root?.FullName ?? throw new DirectoryNotFoundException(),
        "src", "Voucha.Client.App", "Pages", file));
  }
}
