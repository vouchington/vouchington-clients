using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class TagManagementPageSourceTests
{
  [Fact]
  public void RelationVoteButtonsBindPolicyCurrentVoteAndMutationState()
  {
    var source = AppSource.Read("Pages", "TagManagementPage.Templates.cs");

    Assert.Contains("TagRelationVoteEligibilityBinding(choice)", source, StringComparison.Ordinal);
    Assert.Contains("TagRelationVoteEligibilityBinding(null)", source, StringComparison.Ordinal);
    Assert.Contains("nameof(TagRelationRow.MyVote)", source, StringComparison.Ordinal);
    Assert.Contains("nameof(TagManagementViewModel.IsLoading)", source, StringComparison.Ordinal);
    Assert.Contains("nameof(VotePolicyVersion)", source, StringComparison.Ordinal);
    Assert.Contains("sender is Button { IsEnabled: true, CommandParameter: TagRelationRow row }", source, StringComparison.Ordinal);
  }
}
