using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.NewsFeeds;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class NewsFeedsStoryDiscussionVisibilityTests
{
  [Fact]
  public async Task ContinuationOnlyStoryUsesReactiveDiscussionVisibility()
  {
    var related = new StoryRelatedArticles("story-1", "primary", [], new PageInfo("after", true, null), UiLocalization.English);
    var primary = new NewsFeedItem("primary", "Article", "Source", "Summary", null, DateTimeOffset.UnixEpoch,
        StoryId: "story-1", StoryArticles: related);
    var model = new NewsFeedsViewModel(new Service(primary));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(related.HasItems);
    Assert.True(primary.CanStartStoryDiscussion);
    Assert.True(related.CanExpand);
    var changed = new List<string?>();
    related.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

    foreach (var page in new[] { "NewsFeedsPage.xaml", "StoryRelatedArticlesView.xaml" })
    {
      var document = PageDocument(page);
      var button = document.Descendants().Single(element =>
          element.Attributes().Any(attribute => attribute.Name.LocalName == "Clicked" && attribute.Value == "OnStartStoryDiscussionClicked"));
      var paths = button.Descendants().Where(element => element.Name.LocalName == "Binding")
          .Select(element => element.Attributes().Single(attribute => attribute.Name.LocalName == "Path").Value);
      Assert.Equal(["CanStartStoryDiscussion", "StoryArticles.CanExpand"], paths);
    }

    await model.LoadMoreStoryArticlesAsync(model.Items[0], TestContext.Current.CancellationToken);
    Assert.False(related.CanExpand);
    Assert.False(primary.CanStartStoryDiscussion);
    Assert.Contains(nameof(StoryRelatedArticles.CanExpand), changed);
  }

  private static XDocument PageDocument(string page, [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return XDocument.Load(Path.Combine(
        root?.FullName ?? throw new DirectoryNotFoundException(),
        "src", "Voucha.Client.App", "Pages", page));
  }

  private sealed class Service(NewsFeedItem primary) : INewsFeedService, IStoryRelatedArticlesService
  {
    public Task<NewsFeedPage> GetNewsFeedPageAsync(NewsFeedScope scope, NewsFeedSourceType sourceFeedType,
        string? after = null, int limit = 20, CancellationToken cancellationToken = default) =>
        Task.FromResult(new NewsFeedPage([primary], new PageInfo(null, false, null)));
    public Task<NewsFeedPage> GetNewsFeedPageAsync(NewsFeedScope scope, string? after = null, int limit = 20,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new NewsFeedPage([primary], new PageInfo(null, false, null)));
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NewsFeedItem>>([primary]);
    public Task<IReadOnlyList<NewsFeedItem>> GetNewsFeedItemsAsync(NewsFeedScope scope,
        NewsFeedSourceType sourceFeedType, CancellationToken cancellationToken = default) =>
        GetNewsFeedItemsAsync(scope, cancellationToken);
    public Task<NewsFeedPage> GetStoryRelatedArticlesPageAsync(string storyId, string primaryItemId,
        string? after, CancellationToken cancellationToken = default) =>
        Task.FromResult(new NewsFeedPage([], new PageInfo(null, false, null)));
    public Task SetSourceFollowAsync(string sourceId, bool following, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task SetTopicFollowAsync(string topicId, bool following, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task SetReadAsync(string itemId, bool read, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
  }
}
