using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class DirectMessagesPageRoutingTests
{
  [Fact]
  public void ExactNewMessageRouteBranchPrecedesBlankConversationBranch()
  {
    var source = File.ReadAllText(RepoPath(
        "dotnet-clients",
        "src",
        "Voucha.Client.App",
        "Pages",
        "DirectMessagesPage.Routing.cs"));

    var newMessageRouteIndex = source.IndexOf(
        "if (string.Equals(path, \"/messages/new\", StringComparison.Ordinal))",
        StringComparison.Ordinal);
    var blankConversationIndex = source.IndexOf(
        "if (string.IsNullOrWhiteSpace(conversationId))",
        StringComparison.Ordinal);

    Assert.True(newMessageRouteIndex >= 0);
    Assert.True(blankConversationIndex >= 0);
    Assert.True(newMessageRouteIndex < blankConversationIndex);
    Assert.Contains("viewModel.ClearSelectedConversationState()", source, StringComparison.Ordinal);
  }

  private static string RepoPath(params string[] parts)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
      var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
      if (File.Exists(candidate))
      {
        return candidate;
      }

      directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}
