using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class VoteClearPageBindingTests
{
  [Theory]
  [InlineData("PostsPage.xaml")]
  [InlineData("TopicsPage.xaml")]
  [InlineData("NewsFeedsPage.xaml")]
  public void ClearVoteRequiresBothTheCurrentRowBallotAndSessionEligibility(string page)
  {
    var xaml = ReadPage(page);

    Assert.Contains("<pages:VoteClearEligibilityConverter x:Key=\"VoteClearEligibilityConverter\" />", xaml, StringComparison.Ordinal);
    Assert.Contains("<MultiBinding Converter=\"{StaticResource VoteClearEligibilityConverter}\">", xaml, StringComparison.Ordinal);
    Assert.Contains("<Binding Path=\"CurrentVoteChoice\" />", xaml, StringComparison.Ordinal);
    Assert.Contains("<Binding Source=\"{x:Reference PageRoot}\" Path=\"BindingContext.CanClearVote\" />", xaml, StringComparison.Ordinal);
    Assert.Equal(1, xaml.Split("Clicked=\"OnClearVoteClicked\"", StringSplitOptions.None).Length - 1);
  }

  private static string ReadPage(string file, [CallerFilePath] string sourceFile = "") =>
      File.ReadAllText(RepoPath(sourceFile, "dotnet-clients", "src", "Voucha.Client.App", "Pages", file));

  private static string RepoPath(string sourceFile, params string[] parts)
  {
    foreach (var start in new[] { Path.GetDirectoryName(sourceFile)!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
      for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
      {
        var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
        if (File.Exists(candidate)) return candidate;
      }
    }

    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}
