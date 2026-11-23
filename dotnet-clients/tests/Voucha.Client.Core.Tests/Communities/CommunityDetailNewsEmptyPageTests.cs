using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Pagination;
using Voucha.Client.Core.Tests.Api;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityDetailNewsEmptyPageTests
{
  private const int Cap = EmptyPageContinuation.MaxConsecutiveEmptyPages;

  [Fact]
  public async Task EmptyFirstNewsPageWithNextPageContinuesAndShowsLaterRows()
  {
    var (viewModel, service) = await LoadNewsAsync(Empty("c1"), Row("news-2", null, hasNext: false));

    Assert.Equal(["news-2"], viewModel.News.Select(row => row.Id));
    Assert.False(viewModel.HasMoreCommunityList);
    Assert.Equal([null, "c1"], NewsRequests(service));
  }

  [Fact]
  public async Task EmptyLaterNewsPageWithNextPageContinues()
  {
    var (viewModel, service) = await LoadNewsAsync(
        Row("news-1", "c1"), Empty("c2"), Row("news-3", null, hasNext: false));

    await viewModel.LoadMoreCommunityListAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["news-1", "news-3"], viewModel.News.Select(row => row.Id));
    Assert.Equal([null, "c1", "c2"], NewsRequests(service));
    Assert.False(viewModel.HasMoreCommunityList);
  }

  [Fact]
  public async Task ConsecutiveEmptyNewsPagesStopAtCapThenManualLoadMoreContinues()
  {
    var pages = Enumerable.Range(1, Cap).Select(index => Empty($"c{index}")).ToList();
    pages.Add(Row("late", null, hasNext: false));
    var (viewModel, service) = await LoadNewsAsync([.. pages]);

    Assert.Empty(viewModel.News);
    Assert.Equal(Cap, NewsRequests(service).Count);
    Assert.True(viewModel.HasMoreCommunityList);
    Assert.True(viewModel.CanLoadMoreCommunityList);
    Assert.False(viewModel.CanAutomaticallyLoadCommunityList);

    await viewModel.LoadMoreCommunityListAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["late"], viewModel.News.Select(row => row.Id));
    Assert.False(viewModel.HasMoreCommunityList);
  }

  [Fact]
  public async Task EmptyNewsPageWithoutNextPageIsTrueEmptyState()
  {
    var (viewModel, service) = await LoadNewsAsync(Empty("stale-cursor", hasNext: false));

    Assert.Empty(viewModel.News);
    Assert.False(viewModel.HasMoreCommunityList);
    Assert.False(viewModel.CanLoadMoreCommunityList);
    Assert.Single(NewsRequests(service));
  }

  [Fact]
  public async Task NewsPageWithCursorButNoNextPageDoesNotOfferLoadMore()
  {
    var (viewModel, _) = await LoadNewsAsync(Row("news-1", "last-cursor", hasNext: false));

    Assert.Equal(["news-1"], viewModel.News.Select(row => row.Id));
    Assert.False(viewModel.HasMoreCommunityList);
  }

  private static async Task<(CommunityDetailViewModel, ScriptedCommunitiesService)> LoadNewsAsync(
      params RssFeedItemsFeedResponse[] pages)
  {
    var service = new ScriptedCommunitiesService();
    service.DetailResponses.Enqueue(CreateDetailResponse(
        hasMembership: true, hasPendingApplication: false, membershipRole: "owner"));
    service.CountsResponses.Enqueue(new CommunityListItemCountsResponse(1, 2, 3, 4, 5));
    service.MembersResponses.Enqueue(Fixture<CommunityMembersResponse>("web.communities.members.default"));
    service.PostsResponses.Enqueue(Fixture<CommunityPostsResponse>("web.communities.posts.default"));
    foreach (var page in pages) service.NewsResponses.Enqueue(page);
    var viewModel = new CommunityDetailViewModel(
        service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadSurfaceAsync(
        "community-1", CommunityDetailSurfaceSection.News, TestContext.Current.CancellationToken);
    return (viewModel, service);
  }

  private static List<string?> NewsRequests(ScriptedCommunitiesService service) =>
      [.. service.ForwardPageRequests
          .Where(request => request.Section == CommunityDetailSurfaceSection.News)
          .Select(request => request.After)];

  private static RssFeedItemsFeedResponse Empty(string cursor, bool hasNext = true) =>
      Template() with { Results = [], PageInfo = new PageInfo(cursor, hasNext, null) };

  private static RssFeedItemsFeedResponse Row(string id, string? cursor, bool hasNext = true)
  {
    var template = Template();
    var item = template.RssFeedItems.Values.First() with { Id = id };
    return template with
    {
      Results = [new EntityReference(null, null, id, null, null, null, null, null, null, null, null, null)],
      RssFeedItems = new Dictionary<string, RssFeedItem> { [id] = item },
      PageInfo = new PageInfo(cursor, hasNext, null),
    };
  }

  private static RssFeedItemsFeedResponse Template() =>
      JsonSerializer.Deserialize<RssFeedItemsFeedResponse>(
          ApiFixtureLoader.LoadResponse("web.communities.news.default"), VouchaApiJson.Options)!;

  private static T Fixture<T>(string id) =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(id), VouchaApiJson.Options)
      ?? throw new InvalidOperationException($"{id} did not deserialize as {typeof(T).Name}.");
}
