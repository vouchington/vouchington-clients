using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.LandingPages;

public sealed class LandingPagesViewModelMutationResilienceTests
{
  [Fact]
  public async Task LoadAsyncClearsSelectionWhenNoPagesExist()
  {
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
    };
    var viewModel = new LandingPagesViewModel(service)
    {
      Title = "Stale title",
      Subtitle = "Stale subtitle",
      Slug = "stale-slug",
    };
    viewModel.AddLink("Stale", new Uri("https://example.com/stale"));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.SelectedPage);
    Assert.Equal("", viewModel.Title);
    Assert.Equal("", viewModel.Subtitle);
    Assert.Equal("", viewModel.Slug);
    Assert.Empty(viewModel.DraftItems);
  }

  [Fact]
  public async Task MutationFailuresSetErrorAndReturnFalse()
  {
    var service = new RecordingLandingPagesService { ThrowOnCreate = true };
    var viewModel = new LandingPagesViewModel(service);

    var created = await viewModel.CreateAsync(
        "Broken",
        null,
        "broken",
        TestContext.Current.CancellationToken);

    Assert.False(created);
    Assert.Equal(LoadState.Error, viewModel.State);
    Assert.Contains("create failed", viewModel.ErrorMessage, StringComparison.Ordinal);
    Assert.Null(viewModel.SelectedPage);
  }

  [Fact]
  public async Task MetadataMutationsPreserveDraftItemsWhenResponsesOmitItems()
  {
    var initialItem = new LandingPageLinkItem("item-link-1", "Newsletter", new Uri("https://example.com/newsletter"));
    var initialPage = MakePage([initialItem]);
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([initialPage], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(initialPage),
      UpdateResponse = new LandingPageDetailResponse(MakePage(title: "Updated Links")),
      DefaultResponse = new LandingPageDetailResponse(MakePage(title: "Updated Links", isDefault: true)),
    };
    var viewModel = new LandingPagesViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.Title = "Updated Links";

    var savedDetails = await viewModel.SaveDetailsAsync(TestContext.Current.CancellationToken);
    viewModel.Title = "Draft title";
    viewModel.Subtitle = "Draft subtitle";
    viewModel.Slug = "draft-slug";
    var madeDefault = await viewModel.SetDefaultAsync(TestContext.Current.CancellationToken);

    Assert.True(savedDetails);
    Assert.True(madeDefault);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Equal("Draft title", viewModel.Title);
    Assert.Equal("Draft subtitle", viewModel.Subtitle);
    Assert.Equal("draft-slug", viewModel.Slug);
    Assert.Equal("Draft title", viewModel.SelectedPage?.Title);
    Assert.Equal("Draft subtitle", viewModel.SelectedPage?.Subtitle);
    Assert.Equal("draft-slug", viewModel.SelectedPage?.Slug);
    Assert.Single(viewModel.DraftItems);
    Assert.Equal("item-link-1", viewModel.DraftItems.Single().Id);
    Assert.Equal("item-link-1", viewModel.SelectedPage?.Items?.Single().Id);
  }

  [Fact]
  public async Task LoadAsyncPrefersInitialSlugOverDefaultSelection()
  {
    var travelPage = MakePage(id: "landing-page-2", title: "Travel Stack", slug: "travel");
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse(
          [MakePage(isDefault: true), travelPage],
          new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(travelPage),
    };
    var viewModel = new LandingPagesViewModel(service);
    viewModel.SetInitialSlug("travel");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("landing-page-2", service.LastDetailPageId);
    Assert.Equal("travel", viewModel.SelectedPage?.Slug);
    Assert.Equal("Travel Stack", viewModel.Title);
  }

  [Fact]
  public async Task LoadAsyncPrefersInitialSlugOverCachedSelection()
  {
    var defaultPage = MakePage(isDefault: true);
    var travelPage = MakePage(id: "landing-page-2", title: "Travel Stack", slug: "travel");
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([defaultPage, travelPage], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(defaultPage),
    };
    var viewModel = new LandingPagesViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    service.DetailResponse = new LandingPageDetailResponse(travelPage);
    viewModel.SetInitialSlug("travel");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("landing-page-2", service.LastDetailPageId);
    Assert.Equal("travel", viewModel.SelectedPage?.Slug);
    Assert.Equal("Travel Stack", viewModel.Title);
  }

  [Fact]
  public async Task LoadAsyncClearsCachedSelectionMissingFromRefreshedPages()
  {
    var defaultPage = MakePage(isDefault: true);
    var travelPage = MakePage(id: "landing-page-2", title: "Travel Stack", slug: "travel");
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([defaultPage, travelPage], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(defaultPage),
    };
    var viewModel = new LandingPagesViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    service.DetailResponse = new LandingPageDetailResponse(travelPage);
    await viewModel.SelectPageAsync("landing-page-2", TestContext.Current.CancellationToken);

    service.PagesResponse = new LandingPagesResponse([defaultPage], new PageInfo(null, false, null));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("landing-page-2", service.LastDetailPageId);
    Assert.Null(viewModel.SelectedPage);
    Assert.Equal("", viewModel.Title);
    Assert.Empty(viewModel.DraftItems);
  }

  [Fact]
  public async Task LoadAsyncClearsSelectionWhenInitialSlugIsMissing()
  {
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse(
          [
            MakePage(isDefault: true),
            MakePage(id: "landing-page-2", title: "Travel Stack", slug: "travel"),
          ],
          new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
    };
    var viewModel = new LandingPagesViewModel(service);
    viewModel.SetInitialSlug("missing");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.SelectedPage);
    Assert.Equal("", viewModel.Title);
    Assert.Equal("", viewModel.Subtitle);
    Assert.Equal("", viewModel.Slug);
    Assert.Empty(viewModel.DraftItems);
    Assert.Null(service.LastDetailPageId);
  }

  [Fact]
  public async Task DeleteSelectedAsyncClearsSelectionForInitialSlugRoutes()
  {
    var travelPage = MakePage(id: "landing-page-2", title: "Travel Stack", slug: "travel");
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([MakePage(isDefault: true), travelPage], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(travelPage),
    };
    var viewModel = new LandingPagesViewModel(service);
    viewModel.SetInitialSlug("travel");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.PagesResponse = new LandingPagesResponse([MakePage(isDefault: true)], new PageInfo(null, false, null));

    var deleted = await viewModel.DeleteSelectedAsync(TestContext.Current.CancellationToken);

    Assert.True(deleted);
    Assert.Equal("landing-page-2", service.LastDeletedPageId);
    Assert.Null(viewModel.SelectedPage);
    Assert.Equal("", viewModel.Title);
    Assert.Equal("", viewModel.Slug);
  }

  [Fact]
  public async Task DeleteSelectedAsyncSelectsNextPageForIndexRoutes()
  {
    var defaultPage = MakePage(isDefault: true);
    var travelPage = MakePage(id: "landing-page-2", title: "Travel Stack", slug: "travel");
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([defaultPage, travelPage], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(defaultPage),
    };
    var viewModel = new LandingPagesViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.PagesResponse = new LandingPagesResponse([travelPage], new PageInfo(null, false, null));
    service.DetailResponse = new LandingPageDetailResponse(travelPage);

    var deleted = await viewModel.DeleteSelectedAsync(TestContext.Current.CancellationToken);

    Assert.True(deleted);
    Assert.Equal("landing-page-1", service.LastDeletedPageId);
    Assert.Equal("landing-page-2", service.LastDetailPageId);
    Assert.Equal("travel", viewModel.SelectedPage?.Slug);
  }

  [Fact]
  public async Task AddLinkUsesUniqueDraftIdsAfterRemoval()
  {
    Assert.False(new LandingPagesViewModel(new RecordingLandingPagesService())
        .AddLink("Missing selection", new Uri("https://example.com/missing")));
    var viewModel = await CreateDraftableViewModel();

    Assert.True(viewModel.AddLink("First", new Uri("https://example.com/first")));
    var firstId = viewModel.DraftItems.Single().Id;
    viewModel.RemoveItem(viewModel.DraftItems.Single());
    Assert.True(viewModel.AddLink("Second", new Uri("https://example.com/second")));

    Assert.NotEqual(firstId, viewModel.DraftItems.Single().Id);
  }

  [Fact]
  public async Task AddLinkRejectsNonHttpAndFragmentUrls()
  {
    Assert.False(new LandingPagesViewModel(new RecordingLandingPagesService())
        .AddLink("Missing selection", "https://example.com/missing"));
    var viewModel = await CreateDraftableViewModel();

    Assert.False(viewModel.AddLink("Mail", new Uri("mailto:hello@example.com")));
    Assert.False(viewModel.AddLink("Fragment", new Uri("https://example.com/page#pricing")));
    Assert.False(viewModel.AddLink("Bad scheme", "httpx://example.com"));
    Assert.False(viewModel.AddLink(new string('a', 101), "https://example.com/path"));
    Assert.False(viewModel.AddLink("Docs", "https://example.com/" + new string('a', 2029)));
    Assert.False(viewModel.AddLink(new string('a', 101), new Uri("https://example.com/path")));
    Assert.False(viewModel.AddLink("Docs", new Uri("https://example.com/" + new string('a', 2029))));
    Assert.True(viewModel.AddLink(new string('a', 100), "https://example.com/" + new string('a', 2028)));
    Assert.True(viewModel.AddLink("  Docs  ", "  https://example.com/docs  "));

    Assert.Equal(2, viewModel.DraftItems.Count);
    var item = Assert.IsType<LandingPageLinkItem>(viewModel.DraftItems.Last());
    Assert.Equal("Docs", item.Label);
    Assert.Equal(new Uri("https://example.com/docs"), item.Url);
  }

  [Fact]
  public async Task MoveItemReordersDraftItemsWithinBounds()
  {
    var viewModel = await CreateDraftableViewModel();
    Assert.True(viewModel.AddLink("First", new Uri("https://example.com/first")));
    Assert.True(viewModel.AddLink("Second", new Uri("https://example.com/second")));
    Assert.True(viewModel.AddLink("Third", new Uri("https://example.com/third")));

    viewModel.MoveItem(viewModel.DraftItems[1], -1);
    viewModel.MoveItem(viewModel.DraftItems[2], 1);

    Assert.Equal(["Second", "First", "Third"], viewModel.DraftItems.Cast<LandingPageLinkItem>().Select(item => item.Label));
  }

  [Fact]
  public async Task SaveDetailsAsyncUpdatesInitialSlugAfterSlugRename()
  {
    var originalPage = new LandingPage(
        "landing-page-1",
        "user-abc",
        "Featured Links",
        null,
        "featured",
        true,
        DateTimeOffset.Parse("2026-06-28T10:00:00Z"),
        DateTimeOffset.Parse("2026-06-29T10:00:00Z"));
    var renamedPage = originalPage with { Slug = "featured-renamed", UpdatedAt = DateTimeOffset.Parse("2026-06-30T10:00:00Z") };
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([originalPage], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(originalPage),
      UpdateResponse = new LandingPageDetailResponse(renamedPage),
    };
    var viewModel = new LandingPagesViewModel(service);
    viewModel.SetInitialSlug("featured");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.Slug = "featured-renamed";

    var saved = await viewModel.SaveDetailsAsync(TestContext.Current.CancellationToken);

    service.PagesResponse = new LandingPagesResponse([renamedPage], new PageInfo(null, false, null));
    service.DetailResponse = new LandingPageDetailResponse(renamedPage);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(saved);
    Assert.Equal("featured-renamed", service.LastUpdateBody?.Slug);
    Assert.Equal("landing-page-1", service.LastDetailPageId);
    Assert.Equal("featured-renamed", viewModel.SelectedPage?.Slug);
  }

  [Fact]
  public async Task CreateAsyncIgnoresRepeatedCallsWhileLoading()
  {
    var createGate = new TaskCompletionSource<LandingPageDetailResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var createdPage = MakePage(title: "New page");
    var service = new RecordingLandingPagesService
    {
      CreateGate = createGate,
      PagesResponse = new LandingPagesResponse([createdPage], new PageInfo(null, false, null)),
    };
    var viewModel = new LandingPagesViewModel(service);

    var firstCreate = viewModel.CreateAsync("New page", null, "new-page", TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsLoading);
    var secondCreate = await viewModel.CreateAsync("Ignored", null, "ignored", TestContext.Current.CancellationToken);

    createGate.SetResult(new LandingPageDetailResponse(createdPage));
    Assert.True(await firstCreate);
    Assert.False(secondCreate);
    Assert.Equal(1, service.CreateCalls);
  }

  private sealed class RecordingLandingPagesService : ILandingPagesService
  {
    public LandingPagesResponse? PagesResponse { get; set; }

    public LandingPageCandidatesResponse? CandidatesResponse { get; set; }

    public LandingPageDetailResponse? DetailResponse { get; set; }

    public LandingPageDetailResponse? UpdateResponse { get; set; }

    public LandingPageDetailResponse? DefaultResponse { get; set; }

    public bool ThrowOnCreate { get; init; }

    public int CreateCalls { get; private set; }

    public TaskCompletionSource<LandingPageDetailResponse>? CreateGate { get; set; }

    public string? LastDetailPageId { get; private set; }

    public string? LastDeletedPageId { get; private set; }

    public UpdateLandingPageBody? LastUpdateBody { get; private set; }

    public Task<LandingPagesResponse> FetchPagesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(PagesResponse ?? throw new InvalidOperationException("Missing pages response."));

    public Task<LandingPageCandidatesResponse> FetchCandidatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CandidatesResponse ?? throw new InvalidOperationException("Missing candidates response."));

    public Task<LandingPageAnalyticsResponse> FetchMyLandingPageAnalyticsAsync(
        string pageId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<LandingPagesResponse> FetchAdminUserLandingPagesAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AdminLandingPageAnalyticsResponse> FetchAdminLandingPageAnalyticsAsync(
        string pageId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public async Task<LandingPageDetailResponse> FetchDetailAsync(
        string pageId,
        CancellationToken cancellationToken = default)
    {
      LastDetailPageId = pageId;
      return await Task.FromResult(
          DetailResponse ?? throw new InvalidOperationException("Unexpected detail fetch."))
          .ConfigureAwait(false);
    }

    public Task<LandingPageDetailResponse> CreateAsync(
        CreateLandingPageBody body,
        CancellationToken cancellationToken = default)
    {
      CreateCalls++;
      if (ThrowOnCreate)
      {
        throw new InvalidOperationException("create failed");
      }

      return CreateGate?.Task ?? throw new InvalidOperationException("Unexpected create.");
    }

    public Task<LandingPageDetailResponse> UpdateAsync(
        string pageId,
        UpdateLandingPageBody body,
        CancellationToken cancellationToken = default)
    {
      LastUpdateBody = body;
      return Task.FromResult(UpdateResponse ?? throw new InvalidOperationException("Unexpected update."));
    }

    public Task<LandingPageDetailResponse> SetDefaultAsync(string pageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(DefaultResponse ?? throw new InvalidOperationException("Unexpected default update."));

    public Task DeleteAsync(string pageId, CancellationToken cancellationToken = default)
    {
      LastDeletedPageId = pageId;
      return Task.CompletedTask;
    }

    public Task<LandingPageDetailResponse> ReplaceItemsAsync(
        string pageId,
        ReplaceLandingPageItemsBody body,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Unexpected item replacement.");
  }

  private static LandingPage MakePage(
      IReadOnlyList<LandingPageItem>? items = null,
      string id = "landing-page-1",
      string title = "Featured Links",
      string slug = "featured",
      bool isDefault = false) =>
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

  private static async Task<LandingPagesViewModel> CreateDraftableViewModel()
  {
    var service = new RecordingLandingPagesService
    {
      PagesResponse = new LandingPagesResponse([MakePage()], new PageInfo(null, false, null)),
      CandidatesResponse = new LandingPageCandidatesResponse(new LandingPageCandidates(false, [], [], [])),
      DetailResponse = new LandingPageDetailResponse(MakePage()),
    };
    var viewModel = new LandingPagesViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    return viewModel;
  }
}
