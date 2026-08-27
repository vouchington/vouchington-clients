using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class TagManagementPageSourceTests
{
  [Fact]
  public void RelationVoteButtonsBindPolicyCurrentVoteAndMutationState()
  {
    var source = AppSource("TagManagementPage.Templates.cs");

    Assert.Contains("TagRelationVoteEligibilityBinding(choice)", source, StringComparison.Ordinal);
    Assert.Contains("TagRelationVoteEligibilityBinding(null)", source, StringComparison.Ordinal);
    Assert.Contains("nameof(TagRelationRow.MyVote)", source, StringComparison.Ordinal);
    Assert.Contains("nameof(TagManagementViewModel.IsLoading)", source, StringComparison.Ordinal);
    Assert.Contains("nameof(VotePolicyVersion)", source, StringComparison.Ordinal);
    Assert.Contains("sender is Button { IsEnabled: true, CommandParameter: TagRelationRow row }", source, StringComparison.Ordinal);
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
