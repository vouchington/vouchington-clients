using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Tests.LandingPages;
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
    using var locale = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    using var model = new ProfileViewModel(new RecordingSettingsService(), new RecordingLandingPagesService(),
        posts, new RecordingImageUploadService(), new AppConfig(new Uri("https://api.example.test")),
        new UiLocalization(locale), locale);
    await model.LoadPublicAsync("alice", cancellationToken: TestContext.Current.CancellationToken);
    var english = Assert.Single(model.HistoryItems);
    Assert.Equal("Seen on Hacker News · 1 · Mentioned · 2", english.HackerNews.Summary);
    locale.ApplySavedLocale("es");
    var localized = Assert.Single(model.HistoryItems);
    Assert.NotSame(english, localized);
    Assert.NotEqual("Seen on Hacker News · 1 · Mentioned · 2", localized.HackerNews.Summary);
  }
}
