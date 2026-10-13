using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelTests
{
  [Fact]
  public async Task SessionViewerChangeReprojectsLoadedRejectedHistoryWithoutReload()
  {
    var posts = new RecordingPostsService
    {
      FetchPostsAsyncOverride = (_, _) => Task.FromResult(
          MakeHistoryResponse("post-1", "review", "First review") with
          {
            Posts = new Dictionary<string, Post>
            {
              ["post-1"] = new Post("post-1", "review", "First review", "Private body", "user-1",
                  ClearanceStatus: "rejected"),
            },
          }),
    };
    var viewModel = NewViewModel(new RecordingSettingsService(), posts);
    viewModel.SetCurrentViewer("staff", "staff", isStaffViewer: true);
    await viewModel.LoadOwnAsync(TestContext.Current.CancellationToken);
    Assert.True(Assert.Single(viewModel.HistoryItems).ShowBodyContent);
    var fetches = posts.FetchPostsCallCount;

    viewModel.SetCurrentViewer(null, null, isStaffViewer: false);

    var row = Assert.Single(viewModel.HistoryItems);
    Assert.False(row.ShowBodyContent);
    Assert.True(row.ShowNotice);
    Assert.False(row.ShowEmbed);
    Assert.Equal(fetches, posts.FetchPostsCallCount);
  }
}
