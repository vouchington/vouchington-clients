using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Xunit;

namespace Voucha.Client.Core.Tests.LandingPages;

public sealed class LandingPagesViewModelRegressionTests
{
  [Fact]
  public async Task SelectPageAsyncIsIgnoredWhileAnotherMutationIsLoading()
  {
    var createGate = new TaskCompletionSource<LandingPageDetailResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingLandingPagesService
    {
      CreateGate = createGate,
      PagesResponse = new LandingPagesResponse([MakePage("landing-page-1", "Featured Links", "featured", true)], new PageInfo(null, false, null)),
    };
    var viewModel = new LandingPagesViewModel(service);

    var createTask = viewModel.CreateAsync("New page", null, "new-page", TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsLoading);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, service.FetchPagesCalls);

    await viewModel.SelectPageAsync("landing-page-1", TestContext.Current.CancellationToken);

    Assert.Equal(0, service.FetchDetailCalls);

    createGate.SetResult(new LandingPageDetailResponse(MakePage("landing-page-2", "New page", "new-page", false)));
    Assert.True(await createTask);
  }

  [Fact]
  public async Task CreateAsyncUpdatesInitialSlugBeforeReloadingSlugRoute()
  {
    var createdPage = MakePage("landing-page-2", "Travel Guide", "travel-guide", false);
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([createdPage], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      CreateResponse = new LandingPageDetailResponse(createdPage),
      DetailResponse = new LandingPageDetailResponse(createdPage),
    };
    var viewModel = new LandingPagesViewModel(service);
    viewModel.SetInitialSlug("travel");

    Assert.True(await viewModel.CreateAsync("Travel Guide", null, "travel-guide", TestContext.Current.CancellationToken));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("landing-page-2", service.LastDetailPageId);
    Assert.Equal("travel-guide", viewModel.SelectedPage?.Slug);
  }

  [Fact]
  public async Task LoadAsyncDisablesCreateWhenCandidatesDoNotAllowCreation()
  {
    var page = MakePage("landing-page-1", "Featured Links", "featured", true);
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([page], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(page),
    };
    var viewModel = new LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanCreatePage);
  }

  [Fact]
  public async Task SaveContentAsyncPreservesDetailDraftFieldsWhenResponseChangesMetadata()
  {
    var initialPage = MakePage(
        "landing-page-1",
        "Featured Links",
        "featured",
        true,
        [
          new LandingPageLinkItem("item-link-1", "Newsletter", new Uri("https://example.com/newsletter")),
        ]);
    var updatedPage = MakePage(
        "landing-page-1",
        "Server Title",
        "server-slug",
        true,
        [
          new LandingPageLinkItem("item-link-1", "Docs", new Uri("https://example.com/docs")),
        ]);
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([initialPage], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(initialPage),
      ReplaceItemsResponse = new LandingPageDetailResponse(updatedPage),
    };
    var viewModel = new LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.Title = "Draft Title";
    viewModel.Subtitle = "Draft Subtitle";
    viewModel.Slug = "draft-slug";
    viewModel.AddLink("Docs", new Uri("https://example.com/docs"));

    var saved = await viewModel.SaveContentAsync(TestContext.Current.CancellationToken);

    Assert.True(saved);
    Assert.Equal("Draft Title", viewModel.Title);
    Assert.Equal("Draft Subtitle", viewModel.Subtitle);
    Assert.Equal("draft-slug", viewModel.Slug);
    Assert.Equal("Draft Title", viewModel.SelectedPage?.Title);
    Assert.Equal("Draft Subtitle", viewModel.SelectedPage?.Subtitle);
    Assert.Equal("draft-slug", viewModel.SelectedPage?.Slug);
    Assert.Equal("Docs", Assert.IsType<LandingPageLinkItem>(viewModel.DraftItems.Single()).Label);
  }

  [Fact]
  public async Task DeleteSelectedAsyncClearsSelectionBeforeRefreshFailure()
  {
    var selectedPage = MakePage("landing-page-1", "Featured Links", "featured", true);
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([selectedPage], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(selectedPage),
    };
    var viewModel = new LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadSelectedAnalyticsAsync(TestContext.Current.CancellationToken);
    service.ThrowOnFetchPages = true;

    var deleted = await viewModel.DeleteSelectedAsync(TestContext.Current.CancellationToken);

    Assert.False(deleted);
    Assert.Equal("landing-page-1", service.LastDeletedPageId);
    Assert.Null(viewModel.SelectedPage);
    Assert.Null(viewModel.SelectedPageAnalytics);
    Assert.False(viewModel.HasSelectedPageAnalyticsSection);
    Assert.Equal("", viewModel.Title);
    Assert.Equal("", viewModel.Subtitle);
    Assert.Equal("", viewModel.Slug);
  }

  private static LandingPage MakePage(
      string id,
      string title,
      string slug,
      bool isDefault,
      IReadOnlyList<LandingPageItem>? items = null) =>
      new(
          id,
          "user-abc",
          title,
          null,
          slug,
          isDefault,
          DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
          DateTimeOffset.Parse("2026-06-29T10:00:00Z"),
          items);

  private sealed class RecordingLandingPagesService : ILandingPagesService
  {
    public LandingPagesResponse? PagesResponse { get; init; }

    public LandingPageCandidatesResponse? CandidatesResponse { get; init; }

    public LandingPageDetailResponse? DetailResponse { get; init; }

    public LandingPageDetailResponse? CreateResponse { get; init; }

    public LandingPageDetailResponse? ReplaceItemsResponse { get; init; }

    public TaskCompletionSource<LandingPageDetailResponse>? CreateGate { get; init; }

    public LandingPageAnalyticsResponse MyLandingPageAnalyticsResponse { get; init; } =
        new(new LandingPageAnalytics(0, 0, 0, 0, [], [], [], new LandingPageConversionFunnel(0, 0, 0, 0)));

    public Func<string, CancellationToken, Task<LandingPageAnalyticsResponse>>? MyLandingPageAnalyticsHandler { get; init; }

    public bool ThrowOnFetchPages { get; set; }

    public int FetchPagesCalls { get; private set; }

    public int FetchDetailCalls { get; private set; }

    public string? LastDetailPageId { get; private set; }

    public string? LastDeletedPageId { get; private set; }

    public Task<LandingPagesResponse> FetchPagesAsync(CancellationToken cancellationToken = default)
    {
      FetchPagesCalls++;
      if (ThrowOnFetchPages)
      {
        throw new InvalidOperationException("boom");
      }

      return Task.FromResult(PagesResponse ?? throw new InvalidOperationException("Missing pages response."));
    }

    public Task<LandingPageCandidatesResponse> FetchCandidatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CandidatesResponse ?? throw new InvalidOperationException("Missing candidates response."));

    public Task<LandingPageAnalyticsResponse> FetchMyLandingPageAnalyticsAsync(
        string pageId,
        CancellationToken cancellationToken = default) =>
        MyLandingPageAnalyticsHandler is not null
            ? MyLandingPageAnalyticsHandler(pageId, cancellationToken)
            : Task.FromResult(MyLandingPageAnalyticsResponse);

    public Task<LandingPagesResponse> FetchAdminUserLandingPagesAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AdminLandingPageAnalyticsResponse> FetchAdminLandingPageAnalyticsAsync(
        string pageId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<LandingPageDetailResponse> FetchDetailAsync(string pageId, CancellationToken cancellationToken = default)
    {
      FetchDetailCalls++;
      LastDetailPageId = pageId;
      return Task.FromResult(DetailResponse ?? throw new InvalidOperationException("Missing detail response."));
    }

    public Task<LandingPageDetailResponse> CreateAsync(
        CreateLandingPageBody body,
        CancellationToken cancellationToken = default) =>
        CreateGate?.Task ?? Task.FromResult(CreateResponse ?? throw new InvalidOperationException("Missing create response."));

    public Task<LandingPageDetailResponse> UpdateAsync(
        string pageId,
        UpdateLandingPageBody body,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Unexpected update.");

    public Task<LandingPageDetailResponse> SetDefaultAsync(string pageId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Unexpected default update.");

    public Task DeleteAsync(string pageId, CancellationToken cancellationToken = default)
    {
      LastDeletedPageId = pageId;
      return Task.CompletedTask;
    }

    public Task<LandingPageDetailResponse> ReplaceItemsAsync(
        string pageId,
        ReplaceLandingPageItemsBody body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ReplaceItemsResponse ?? throw new InvalidOperationException("Missing replace response."));
  }
}
