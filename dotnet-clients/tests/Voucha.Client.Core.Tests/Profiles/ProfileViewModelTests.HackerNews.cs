using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelTests
{
  [Fact]
  public async Task ProfilePostHistoryKeepsHackerNewsSidecarCounts()
  {
    var response = JsonSerializer.Deserialize<PostsFeedResponse>(
        File.ReadAllText(ContractPaths.ApiFixture("responses/native.posts.hacker-news.filtered.json")), VouchaApiJson.Options)!;
    var posts = new RecordingPostsService { FetchPostsAsyncOverride = (_, _) => Task.FromResult(response) };
    var model = NewViewModel(new RecordingSettingsService(), posts);
    await model.LoadPublicAsync("alice", cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal("Seen on Hacker News · 1 · Mentioned · 2", Assert.Single(model.HistoryItems).HackerNews.Summary);
  }
}
