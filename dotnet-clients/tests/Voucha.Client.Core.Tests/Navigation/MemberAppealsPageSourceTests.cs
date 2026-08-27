using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class MemberAppealsPageSourceTests
{
  [Theory]
  [InlineData("/my/appeals", MemberAppealsRoute.Tracking)]
  [InlineData("/my/warnings", MemberAppealsRoute.Warnings)]
  [InlineData("/my/bans", MemberAppealsRoute.Bans)]
  [InlineData("/my/removed-posts", MemberAppealsRoute.RemovedPosts)]
  [InlineData("/my/account-status", MemberAppealsRoute.Suspension)]
  public void MemberRoutesResolveToDedicatedMemberSurface(
      string path,
      MemberAppealsRoute expected)
  {
    Assert.True(MemberAppealsRoutes.TryResolve(path, out var route));
    Assert.Equal(expected, route);
  }

  [Fact]
  public void DedicatedMemberPageIsWiredWithoutStaffAppealProjection()
  {
    var routing = RepoFile("dotnet-clients", "src", "Voucha.Client.App",
        "AppShell.ModerationRoutes.cs");
    var page = RepoFile("dotnet-clients", "src", "Voucha.Client.App", "Pages",
        "MemberAppealsPage.cs");
    var registration = RepoFile("dotnet-clients", "src", "Voucha.Client.App",
        "MauiProgram.ModerationAppeals.cs");

    Assert.Contains("MemberAppealsRoutes.TryResolve", routing, StringComparison.Ordinal);
    Assert.Contains("MemberAppealsPageFactory", routing, StringComparison.Ordinal);
    Assert.Contains("IMemberAppealsService", registration, StringComparison.Ordinal);
    Assert.Contains("MemberAppealDraftStore", registration, StringComparison.Ordinal);
    Assert.DoesNotContain("StaffContext", page, StringComparison.Ordinal);
    Assert.DoesNotContain("InternalNotes", page, StringComparison.Ordinal);
    Assert.DoesNotContain("RecommendedAction", page, StringComparison.Ordinal);
  }

  [Fact]
  public void AccountStatusMovedFromSettingsToMemberModeration()
  {
    var match = NativeRouteCatalog.MatchingRoute("/my/account-status");
    Assert.NotNull(match);
    Assert.Equal(NativeRouteDestinationId.ModerationCases, match.Value.Entry.DestinationId);
  }

  private static string RepoFile(params string[] segments)
  {
    var starts = new[]
    {
      Environment.GetEnvironmentVariable("PWD"),
      Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"),
      Directory.GetCurrentDirectory(),
      AppContext.BaseDirectory,
    }.Where(start => !string.IsNullOrWhiteSpace(start));
    foreach (var start in starts)
    {
      var directory = new DirectoryInfo(start!);
      while (directory is not null)
      {
        var candidate = Path.Combine([directory.FullName, .. segments]);
        if (File.Exists(candidate)) return File.ReadAllText(candidate);
        directory = directory.Parent;
      }
    }
    throw new DirectoryNotFoundException();
  }
}
