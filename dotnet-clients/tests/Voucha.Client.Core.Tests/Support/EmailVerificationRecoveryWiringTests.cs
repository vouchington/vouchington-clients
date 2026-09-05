using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class EmailVerificationRecoveryWiringTests
{
  [Fact]
  public void MauiMutationHandlersUseTheCoordinatorGateHandoff()
  {
    var handoffs = new Dictionary<string, int>
    {
      ["TopicsPage.xaml.cs"] = 2,
      ["TopicDetailPage.xaml.cs"] = 2,
      ["NewsFeedsPage.xaml.cs"] = 2,
      ["NewsFeedsPage.StoryDiscussions.cs"] = 1,
      ["PostsPage.xaml.cs"] = 2,
      ["PostDetailPage.Actions.cs"] = 4,
      ["PostComposePage.xaml.cs"] = 1,
      ["ProfilePage.Voting.cs"] = 2,
      ["OmnisearchPage.xaml.cs"] = 2,
    };

    foreach (var (file, expectedCount) in handoffs)
    {
      var source = File.ReadAllText(RepoPath(
          "dotnet-clients",
          "src",
          "Voucha.Client.App",
          "Pages",
          file));
      Assert.Equal(expectedCount, Count(source, "PresentIfRequestedAsync(this,"));
      Assert.DoesNotContain("TakeEmailVerificationRecoveryRequest", source, StringComparison.Ordinal);
    }
  }

  [Fact]
  public void CoreViewModelsDoNotRetainLegacyRecoveryLatches()
  {
    var coreRoot = RepoPath("dotnet-clients", "src", "Voucha.Client.Core");
    var sources = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
        .Select(File.ReadAllText)
        .ToArray();

    Assert.Equal(9, sources.Count(source => source.Contains(
        "public EmailVerificationGatedMutation EmailVerificationGate { get; } = new();",
        StringComparison.Ordinal)));
    Assert.Equal(11, sources.Sum(source => Count(source, "EmailVerificationGate.RunAsync")));
    Assert.DoesNotContain(sources, source => source.Contains(
        "TakeEmailVerificationRecoveryRequest",
        StringComparison.Ordinal));
    Assert.DoesNotContain(sources, source => source.Contains(
        "CaptureEmailVerificationRecovery",
        StringComparison.Ordinal));
    Assert.DoesNotContain(sources, source => source.Contains(
        "recoveryHandled",
        StringComparison.Ordinal));
  }

  [Fact]
  public void MauiCoordinatorDelegatesConsumptionToTheGate()
  {
    var source = File.ReadAllText(RepoPath(
        "dotnet-clients",
        "src",
        "Voucha.Client.App",
        "EmailVerificationRecoveryCoordinator.cs"));

    Assert.Contains("return await gate.ConsumeRecoveryRequestAsync(", source, StringComparison.Ordinal);
    Assert.Contains("_ => PresentAsync(owner)", source, StringComparison.Ordinal);
    Assert.Contains("owner.Navigation.ModalStack.Any(IsRecoveryModal)", source, StringComparison.Ordinal);
    Assert.Contains("NavigationPage { RootPage: EmailVerificationRecoveryPage }", source, StringComparison.Ordinal);
  }

  [Fact]
  public void MauiHandoffsGuardPostRecoveryWork()
  {
    var postDetail = File.ReadAllText(RepoPath(
        "dotnet-clients", "src", "Voucha.Client.App", "Pages", "PostDetailPage.Actions.cs"));
    Assert.Equal(4, Count(
        postDetail,
        "if (await emailRecovery.PresentIfRequestedAsync(this, binding.ViewModel.EmailVerificationGate))"));
    Assert.Equal(4, Count(postDetail, "return;\n      }\n      binding.RefreshRows();"));

    var storyDiscussion = File.ReadAllText(RepoPath(
        "dotnet-clients", "src", "Voucha.Client.App", "Pages", "NewsFeedsPage.StoryDiscussions.cs"));
    Assert.Contains(
        "if (await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate))\n    {\n      return;\n    }\n    if (result is null)",
        storyDiscussion,
        StringComparison.Ordinal);
  }

  private static int Count(string source, string value) =>
      source.Split(value, StringSplitOptions.None).Length - 1;

  private static string RepoPath(params string[] parts)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
      var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
      if (File.Exists(candidate) || Directory.Exists(candidate))
      {
        return candidate;
      }

      directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}
