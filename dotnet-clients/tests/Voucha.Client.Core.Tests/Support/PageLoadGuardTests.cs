using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class PageLoadGuardTests
{
  [Fact]
  public async Task WaitsForTheLoadBeforeReturning()
  {
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var finished = false;

    var running = PageLoadGuard.RunAsync(async () =>
    {
      await started.Task;
      finished = true;
    });

    Assert.False(running.IsCompleted);
    started.SetResult();
    await running;
    Assert.True(finished);
    Assert.True(running.IsCompletedSuccessfully);
  }

  [Fact]
  public async Task KeepsAFailedLoadInsideTheLifecycle()
  {
    var started = false;

    var running = PageLoadGuard.RunAsync(() =>
    {
      started = true;
      throw new InvalidOperationException("load failed");
    });

    await running;
    Assert.True(started);
    Assert.True(running.IsCompletedSuccessfully);
  }

  [Fact]
  public async Task KeepsCancellationInsideTheLifecycle()
  {
    var running = PageLoadGuard.RunAsync(() => throw new OperationCanceledException("appearance cancelled"));

    await running;
    Assert.True(running.IsCompletedSuccessfully);
  }

  [Fact]
  public async Task RejectsAMissingLoad()
  {
    var thrown = await Assert.ThrowsAsync<ArgumentNullException>(() => PageLoadGuard.RunAsync(null!));

    Assert.Equal("load", thrown.ParamName);
  }

  [Theory]
  [InlineData("BookmarkCollectionPage.cs")]
  [InlineData("EngineeringPostgreSqlPage.xaml.cs")]
  [InlineData("FriendRecommendationsPage.xaml.cs")]
  [InlineData("EngineeringValkeyPage.xaml.cs")]
  [InlineData("EngineeringQueuesPage.xaml.cs")]
  [InlineData("HouseholdPage.xaml.cs")]
  [InlineData("LandingPagesPage.xaml.cs")]
  [InlineData("LandingPageAnalyticsPage.xaml.cs")]
  [InlineData("AiCostsPage.xaml.cs")]
  [InlineData("PointValuationsPage.xaml.cs")]
  [InlineData("NotificationsPage.xaml.cs")]
  [InlineData("TopicsPage.xaml.cs")]
  public void AppearanceLoadsUseTheSharedGuard(string page)
  {
    var source = File.ReadAllText(RepositoryFilePath.Find(
        "dotnet-clients", "src", "Voucha.Client.App", "Pages", page));
    var appearing = OnAppearing(source);

    Assert.Contains("PageLoadGuard.RunAsync", appearing, StringComparison.Ordinal);
    Assert.DoesNotContain("Debug.WriteLine", appearing, StringComparison.Ordinal);
  }

  private static string OnAppearing(string source)
  {
    const string signature = "protected override async void OnAppearing()";
    var start = source.IndexOf(signature, StringComparison.Ordinal);
    Assert.True(start >= 0, "OnAppearing was not found.");
    var open = source.IndexOf('{', start);
    var depth = 0;
    for (var index = open; index < source.Length; index++)
    {
      if (source[index] == '{') depth++;
      else if (source[index] == '}')
      {
        depth--;
        if (depth == 0) return source[start..(index + 1)];
      }
    }

    throw new InvalidOperationException("OnAppearing is not closed.");
  }
}
