using Voucha.Client.Core.Api;
using Voucha.Client.Core.Lists;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Lists;

public sealed partial class ListsViewModelTests
{
  [Fact]
  public async Task LoadAsyncSelectsFirstListAndLoadsItems()
  {
    var (viewModel, handler) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("native.lists.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasLists);
    Assert.True(viewModel.HasSelectedList);
    Assert.False(viewModel.HasError);
    Assert.False(viewModel.IsLoading);
    Assert.Equal("Reading Queue", viewModel.SelectedList?.Name);
    Assert.Equal("list-item-1", viewModel.Items[0].Id);
    Assert.Equal("rss_feed_item", viewModel.Items[0].ProtocolItemType);
    Assert.Collection(
        handler.Requests,
        request => Assert.Equal("/api/v1/lists?limit=25", request.PathAndQuery),
        request => Assert.Equal("/api/v1/lists/list-1/items?limit=25", request.PathAndQuery));
  }

  [Fact]
  public async Task LoadAsyncReconcilesSelectedListWithRefreshedRows()
  {
    var (viewModel, _) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("native.lists.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"),
        ListsWithRenamedListJson,
        ApiFixtureLoader.LoadResponse("native.list-items.default"),
        ListsWithoutSelectedListJson,
        ApiFixtureLoader.LoadResponse("native.list-items.default"));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Renamed Queue", viewModel.SelectedList?.Name);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("list-other", viewModel.SelectedList?.Id);
    Assert.Equal("Other Queue", viewModel.SelectedList?.Name);
    Assert.True(viewModel.HasLists);
  }

  [Fact]
  public async Task SelectFilterAsyncReloadsSelectedListWithMediaType()
  {
    var (viewModel, handler) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("native.lists.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectFilterAsync(ListItemFilter.Watch, TestContext.Current.CancellationToken);

    Assert.True(viewModel.IsWatchSelected);
    Assert.False(viewModel.IsAllSelected);
    Assert.Equal("/api/v1/lists/list-1/items?limit=25&media_type=video", handler.Requests[^1].PathAndQuery);
  }

  [Fact]
  public async Task SelectFilterAsyncExposesItemReloadErrors()
  {
    var (viewModel, _) = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.lists.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.list-items.default")),
        new RecordedResponse("{}", System.Net.HttpStatusCode.TooManyRequests));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectFilterAsync(ListItemFilter.Watch, TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Empty(viewModel.Items);
    Assert.NotNull(viewModel.SelectedList);
  }

  [Fact]
  public async Task ImportCommunityAsyncTrimsSlugAndReloadsItems()
  {
    var (viewModel, handler) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("native.lists.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"),
        ApiFixtureLoader.LoadResponse("native.list-import.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.ImportCommunityAsync(" test-community ", TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/lists/list-1/import", handler.Requests[^2].PathAndQuery);
    Assert.Contains("\"community_slug\":\"test-community\"", handler.Requests[^2].Body!, StringComparison.Ordinal);
    Assert.Equal("/api/v1/lists/list-1/items?limit=25", handler.Requests[^1].PathAndQuery);
  }

  [Fact]
  public async Task CreateListAsyncUsesCreateResponseWithoutFullReload()
  {
    var (viewModel, handler) = CreateViewModel(CreatedListJson);

    await viewModel.CreateListAsync(" Reading ", TestContext.Current.CancellationToken);

    Assert.Collection(
        handler.Requests,
        request =>
        {
          Assert.Equal(HttpMethod.Post, request.Method);
          Assert.Equal("/api/v1/lists", request.PathAndQuery);
        });
    Assert.Equal("list-created", viewModel.SelectedList?.Id);
    Assert.Equal("Reading", viewModel.SelectedList?.Name);
    Assert.Empty(viewModel.Items);
    Assert.True(viewModel.HasLists);
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task UpdateSelectedListAsyncUsesPatchResponseWithoutReloadingItems()
  {
    var (viewModel, handler) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("native.lists.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"),
        UpdatedListJson);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.UpdateSelectedListAsync(
        " Renamed Queue ",
        " Updated description ",
        TestContext.Current.CancellationToken);

    Assert.Equal(3, handler.Requests.Count);
    Assert.Equal(HttpMethod.Patch, handler.Requests[2].Method);
    Assert.Equal("/api/v1/lists/list-1", handler.Requests[2].PathAndQuery);
    Assert.Contains("\"name\":\"Renamed Queue\"", handler.Requests[2].Body!, StringComparison.Ordinal);
    Assert.Contains("\"description\":\"Updated description\"", handler.Requests[2].Body!, StringComparison.Ordinal);
    Assert.Equal("Renamed Queue", viewModel.SelectedList?.Name);
    Assert.Equal("Updated description", viewModel.SelectedList?.Description);
    Assert.Equal("Renamed Queue", viewModel.SelectedListName);
    Assert.Equal("Updated description", viewModel.SelectedListDescription);
    Assert.Equal("list-item-1", viewModel.Items[0].Id);
    Assert.Equal("rss_feed_item", viewModel.Items[0].ProtocolItemType);
  }

  [Fact]
  public async Task SlowUpdateSelectedListAsyncDoesNotStealNewerSelection()
  {
    var handler = new DelayedUpdateHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new ListsViewModel(client);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var update = viewModel.UpdateSelectedListAsync(
        " Renamed Queue ",
        " Updated description ",
        TestContext.Current.CancellationToken);
    await handler.PatchStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await viewModel.SelectListAsync(viewModel.Lists[1], TestContext.Current.CancellationToken);
    handler.ReleasePatch.SetResult();
    await update;

    Assert.Equal("list-2", viewModel.SelectedList?.Id);
    Assert.Equal("Second Queue", viewModel.SelectedList?.Name);
    Assert.Equal("Renamed Queue", viewModel.Lists.Single(list => list.Id == "list-1").Name);
  }

  [Fact]
  public async Task UpdateSelectedListAsyncIgnoresMissingSelectionAndBlankName()
  {
    var unloaded = CreateViewModel(Array.Empty<RecordedResponse>()).ViewModel;

    await unloaded.UpdateSelectedListAsync("Renamed Queue", "Updated description", TestContext.Current.CancellationToken);

    var (viewModel, handler) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("native.lists.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.UpdateSelectedListAsync("   ", "Updated description", TestContext.Current.CancellationToken);

    Assert.Equal(2, handler.Requests.Count);
    Assert.False(viewModel.HasError);
    Assert.Equal("Reading Queue", viewModel.SelectedList?.Name);
    Assert.Equal("Articles to read later", viewModel.SelectedList?.Description);
    Assert.Equal("Reading Queue", viewModel.SelectedListName);
    Assert.Equal("Articles to read later", viewModel.SelectedListDescription);
  }

  [Fact]
  public async Task UpdateSelectedListAsyncClearsDescription()
  {
    var (viewModel, handler) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("native.lists.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"),
        ClearedDescriptionListJson);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.UpdateSelectedListAsync("Reading Queue", "   ", TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Patch, handler.Requests[2].Method);
    Assert.Contains("\"description\":null", handler.Requests[2].Body!, StringComparison.Ordinal);
    Assert.Null(viewModel.SelectedList?.Description);
    Assert.Empty(viewModel.SelectedListDescription);
  }

  [Fact]
  public void SelectedListTextPropertiesIgnoreRepeatedAssignments()
  {
    var viewModel = CreateViewModel(Array.Empty<RecordedResponse>()).ViewModel;
    var changedProperties = new List<string?>();

    viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

    viewModel.SelectedListName = "Reading Queue";
    viewModel.SelectedListName = "Reading Queue";
    viewModel.SelectedListDescription = "Articles to read later";
    viewModel.SelectedListDescription = "Articles to read later";

    Assert.Equal(["SelectedListName", "SelectedListDescription"], changedProperties);
  }

  [Fact]
  public async Task UpdateSelectedListAsyncPreservesSelectionOnFailure()
  {
    var (viewModel, _) = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.lists.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.list-items.default")),
        new RecordedResponse("{}", System.Net.HttpStatusCode.InternalServerError));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.UpdateSelectedListAsync("Renamed Queue", "Updated description", TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Equal("list-1", viewModel.SelectedList?.Id);
    Assert.Equal("Reading Queue", viewModel.SelectedList?.Name);
    Assert.Equal("Articles to read later", viewModel.SelectedList?.Description);
    Assert.Equal("list-item-1", viewModel.Items[0].Id);

    await viewModel.UpdateSelectedListAsync("   ", "Updated description", TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task DeleteSelectedListClearsSelectionAndReloadsLists()
  {
    var (viewModel, handler) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("native.lists.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"),
        "{}",
        ApiFixtureLoader.LoadResponse("native.lists.default"),
        ApiFixtureLoader.LoadResponse("native.list-items.default"));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.DeleteSelectedListAsync(TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
    Assert.Equal("/api/v1/lists/list-1", handler.Requests[2].PathAndQuery);
    Assert.Equal("list-1", viewModel.SelectedList?.Id);
    Assert.NotEmpty(viewModel.Items);
  }

  [Fact]
  public async Task LoadAsyncExposesApiErrors()
  {
    var (viewModel, _) = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.lists.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.list-items.default")),
        new RecordedResponse("{}", System.Net.HttpStatusCode.Unauthorized));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Empty(viewModel.Lists);
    Assert.Empty(viewModel.Items);
    Assert.Null(viewModel.SelectedList);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task ListMutationsExposeApiErrors()
  {
    var create = CreateViewModel(new RecordedResponse("{}", System.Net.HttpStatusCode.InternalServerError)).ViewModel;
    await create.CreateListAsync("Reading", TestContext.Current.CancellationToken);
    Assert.True(create.HasError);

    var delete = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.lists.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.list-items.default")),
        new RecordedResponse("{}", System.Net.HttpStatusCode.InternalServerError)).ViewModel;
    await delete.LoadAsync(TestContext.Current.CancellationToken);
    await delete.DeleteSelectedListAsync(TestContext.Current.CancellationToken);
    Assert.True(delete.HasError);
    Assert.NotNull(delete.SelectedList);

    var import = CreateViewModel(
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.lists.default")),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.list-items.default")),
        new RecordedResponse("{}", System.Net.HttpStatusCode.InternalServerError)).ViewModel;
    await import.LoadAsync(TestContext.Current.CancellationToken);
    await import.ImportCommunityAsync("community", TestContext.Current.CancellationToken);
    Assert.True(import.HasError);
  }

  [Fact]
  public async Task CreateListAsyncIgnoresBlankNames()
  {
    var (viewModel, handler) = CreateViewModel(ApiFixtureLoader.LoadResponse("native.lists.default"));

    await viewModel.CreateListAsync("  ", TestContext.Current.CancellationToken);

    Assert.Empty(handler.Requests);
  }

  private static (ListsViewModel ViewModel, RecordingHandler Handler) CreateViewModel(params string[] responses)
  {
    return CreateViewModel(responses.Select(response => new RecordedResponse(response)).ToArray());
  }

  private static (ListsViewModel ViewModel, RecordingHandler Handler) CreateViewModel(
      params RecordedResponse[] responses)
  {
    var handler = new RecordingHandler(responses);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    return (new ListsViewModel(client), handler);
  }

  private const string CreatedListJson = """
      {
        "list": {
          "id": "list-created",
          "owner_user_id": "user-abc",
          "name": "Reading",
          "description": null,
          "visibility": "private",
          "created_at": "2026-06-28T10:00:00Z",
          "updated_at": "2026-06-28T10:00:00Z",
          "removed_at": null
        }
      }
      """;

  private const string ListsWithRenamedListJson = """
      {
        "results": [{ "id": "list-1" }],
        "page_info": { "has_next_page": false },
        "lists": {
          "list-1": {
            "id": "list-1",
            "owner_user_id": "user-abc",
            "name": "Renamed Queue",
            "description": null,
            "visibility": "private",
            "created_at": "2026-06-28T10:00:00Z",
            "updated_at": "2026-06-29T10:00:00Z",
            "removed_at": null
          }
        }
      }
      """;

  private const string TwoListsJson = """
      {
        "results": [{ "id": "list-1" }, { "id": "list-2" }],
        "page_info": { "has_next_page": false },
        "lists": {
          "list-1": {
            "id": "list-1",
            "owner_user_id": "user-abc",
            "name": "Reading Queue",
            "description": "Articles to read later",
            "visibility": "private",
            "created_at": "2026-06-28T10:00:00Z",
            "updated_at": "2026-06-28T10:00:00Z",
            "removed_at": null
          },
          "list-2": {
            "id": "list-2",
            "owner_user_id": "user-abc",
            "name": "Second Queue",
            "description": null,
            "visibility": "private",
            "created_at": "2026-06-28T10:01:00Z",
            "updated_at": "2026-06-28T10:01:00Z",
            "removed_at": null
          }
        }
      }
      """;

  private const string List2ItemsJson = """
      {
        "results": [{ "id": "list-item-3" }],
        "page_info": { "has_next_page": false },
        "list_items": {
          "list-item-3": {
            "id": "list-item-3",
            "list_id": "list-2",
            "item_type": "post",
            "entity_id": "post-2",
            "order_index": 0,
            "created_at": "2026-06-28T10:07:00Z",
            "media_type": "discussion"
          }
        }
      }
      """;

  private const string ListsWithoutSelectedListJson = """
      {
        "results": [{ "id": "list-other" }],
        "page_info": { "has_next_page": false },
        "lists": {
          "list-other": {
            "id": "list-other",
            "owner_user_id": "user-abc",
            "name": "Other Queue",
            "description": null,
            "visibility": "private",
            "created_at": "2026-06-28T10:00:00Z",
            "updated_at": "2026-06-29T10:00:00Z",
            "removed_at": null
          }
        }
      }
      """;

  private const string UpdatedListJson = """
      {
        "list": {
          "id": "list-1",
          "owner_user_id": "user-abc",
          "name": "Renamed Queue",
          "description": "Updated description",
          "visibility": "private",
          "created_at": "2026-06-28T10:00:00Z",
          "updated_at": "2026-06-29T10:00:00Z",
          "removed_at": null
        }
      }
      """;

  private const string ClearedDescriptionListJson = """
      {
        "list": {
          "id": "list-1",
          "owner_user_id": "user-abc",
          "name": "Reading Queue",
          "description": null,
          "visibility": "private",
          "created_at": "2026-06-28T10:00:00Z",
          "updated_at": "2026-06-29T10:00:00Z",
          "removed_at": null
        }
      }
      """;

}
