using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Lists;

public sealed partial class ListsViewModelTests
{
  [Fact]
  public async Task LoadMoreListsAsyncForwardsOpaqueCursor()
  {
    var firstPage = WithNextPage(ApiFixtureLoader.LoadResponse("native.lists.default"), "lists-next");
    var (viewModel, handler) = CreateViewModel(
        firstPage,
        ApiFixtureLoader.LoadResponse("native.list-items.default"),
        ListsWithRenamedListJson
            .Replace("list-1", "list-2", StringComparison.Ordinal)
            .Replace("Renamed Queue", "Second Queue", StringComparison.Ordinal));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreListsAsync(TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/lists?after=lists-next&limit=25", handler.Requests[^1].PathAndQuery);
    Assert.Equal(new[] { "Reading Queue", "Second Queue" }, viewModel.Lists.Select(row => row.Name));
    Assert.False(viewModel.HasMoreLists);
  }

  [Fact]
  public async Task LoadMoreItemsAsyncAppendsDistinctRows()
  {
    var firstItems = WithNextPage(
        ApiFixtureLoader.LoadResponse("native.list-items.default"), "items-next");
    var (viewModel, handler) = CreateViewModel(
        ApiFixtureLoader.LoadResponse("native.lists.default"), firstItems, List2ItemsJson);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreItemsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(
        "/api/v1/lists/list-1/items?after=items-next&limit=25",
        handler.Requests[^1].PathAndQuery);
    Assert.Equal(
        new[] { "list-item-1", "list-item-2", "list-item-3" },
        viewModel.Items.Select(item => item.Id));
  }

  [Fact]
  public async Task ListContinuationRetainsRowsAndRetriesTheSameCursorWithoutDuplicates()
  {
    var firstPage = WithNextPage(ApiFixtureLoader.LoadResponse("native.lists.default"), "lists-next");
    var (viewModel, handler) = CreateViewModel(
        new RecordedResponse(firstPage),
        new RecordedResponse(ApiFixtureLoader.LoadResponse("native.list-items.default")),
        new RecordedResponse("{}", System.Net.HttpStatusCode.InternalServerError),
        new RecordedResponse(TwoListsJson));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreListsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["list-1"], viewModel.Lists.Select(row => row.Id));
    Assert.True(viewModel.HasListPaginationError);
    Assert.False(viewModel.HasItemPaginationError);
    Assert.True(viewModel.HasMoreLists);

    await viewModel.LoadMoreListsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["list-1", "list-2"], viewModel.Lists.Select(row => row.Id));
    Assert.False(viewModel.HasListPaginationError);
    Assert.False(viewModel.HasMoreLists);
    Assert.Equal(handler.Requests[2].PathAndQuery, handler.Requests[3].PathAndQuery);
  }

  [Fact]
  public async Task CanceledListContinuationRetainsRowsCursorAndRetryability()
  {
    var handler = new CancelingListPageHandler();
    var viewModel = new Voucha.Client.Core.Lists.ListsViewModel(
        new Voucha.Client.Core.Api.VouchaApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await viewModel.LoadMoreListsAsync(cancellation.Token);

    Assert.Equal(["list-1"], viewModel.Lists.Select(row => row.Id));
    Assert.False(viewModel.HasListPaginationError);
    Assert.False(viewModel.IsLoadingListPage);
    Assert.True(viewModel.HasMoreLists);
    Assert.Equal(2, handler.ListRequestCount);
  }

  [Fact]
  public void UnloadedListsDoNotAdvertiseAContinuation()
  {
    var viewModel = CreateViewModel(Array.Empty<RecordedResponse>()).ViewModel;

    Assert.False(viewModel.HasMoreLists);
  }

  [Fact]
  public async Task UnexpectedListCancellationSurfacesAsRetryablePaginationFailure()
  {
    var handler = new CancelingListPageHandler(unexpectedCancellation: true);
    var viewModel = new Voucha.Client.Core.Lists.ListsViewModel(
        new Voucha.Client.Core.Api.VouchaApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreListsAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasListPaginationError);
    Assert.True(viewModel.HasMoreLists);

    await viewModel.LoadMoreListsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["list-1", "list-2"], viewModel.Lists.Select(row => row.Id));
    Assert.False(viewModel.HasListPaginationError);
    Assert.False(viewModel.HasMoreLists);
    Assert.Equal(3, handler.ListRequestCount);
  }

  [Fact]
  public async Task UnexpectedInitialListCancellationSurfacesThroughGlobalErrorAndRetries()
  {
    var handler = new CancelingListPageHandler(unexpectedInitialCancellation: true);
    var viewModel = new Voucha.Client.Core.Lists.ListsViewModel(
        new Voucha.Client.Core.Api.VouchaApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.True(viewModel.HasListPaginationError);
    Assert.False(viewModel.HasMoreLists);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasError);
    Assert.False(viewModel.HasListPaginationError);
    Assert.Equal(["list-1"], viewModel.Lists.Select(row => row.Id));
    Assert.Equal(2, handler.ListRequestCount);
  }

  private static string WithNextPage(string json, string cursor) =>
      json.Replace(
          "\"has_next_page\": false",
          $"\"end_cursor\": \"{cursor}\", \"has_next_page\": true",
          StringComparison.Ordinal);

  private sealed class CancelingListPageHandler : HttpMessageHandler
  {
    private readonly bool unexpectedCancellation;
    private readonly bool unexpectedInitialCancellation;
    private bool didCancelContinuation;
    private bool didCancelInitial;

    public CancelingListPageHandler(
        bool unexpectedCancellation = false,
        bool unexpectedInitialCancellation = false) =>
        (this.unexpectedCancellation, this.unexpectedInitialCancellation) =
        (unexpectedCancellation, unexpectedInitialCancellation);

    public int ListRequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var path = request.RequestUri?.PathAndQuery;
      if (path == "/api/v1/lists?limit=25")
      {
        ListRequestCount++;
        if (unexpectedInitialCancellation && !didCancelInitial)
        {
          didCancelInitial = true;
          return Task.FromCanceled<HttpResponseMessage>(new CancellationToken(true));
        }
        return ResponseAsync(WithNextPage(
            ApiFixtureLoader.LoadResponse("native.lists.default"), "lists-next"), request);
      }
      if (path == "/api/v1/lists/list-1/items?limit=25")
      {
        return ResponseAsync(ApiFixtureLoader.LoadResponse("native.list-items.default"), request);
      }
      if (path == "/api/v1/lists?after=lists-next&limit=25")
      {
        ListRequestCount++;
        if (!didCancelContinuation)
        {
          didCancelContinuation = true;
          return Task.FromCanceled<HttpResponseMessage>(
              unexpectedCancellation ? new CancellationToken(true) : cancellationToken);
        }
        return ResponseAsync(TwoListsJson, request);
      }
      throw new InvalidOperationException($"Unexpected request: {path}");
    }

    private static Task<HttpResponseMessage> ResponseAsync(
        string body,
        HttpRequestMessage request) =>
        Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
          Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
          RequestMessage = request,
        });
  }
}
