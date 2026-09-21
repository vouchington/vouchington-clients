using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Users;
using Xunit;

namespace Voucha.Client.Core.Tests.Users;

public sealed class UsersBrowseViewModelTests
{
  [Fact]
  public async Task SearchForwardsQueryAndFixedLimit()
  {
    var service = new Service();
    var viewModel = new UsersBrowseViewModel(service, new MutableNavigationViewerProvider()) { Query = "alice" };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Equal("alice", service.LastRequest!.Query);
    Assert.Equal(25, service.LastRequest!.Limit);
    Assert.Single(viewModel.Results);
  }

  [Fact]
  public async Task BlankQueryIssuesNoRequest()
  {
    var service = new Service();
    var viewModel = new UsersBrowseViewModel(service, new MutableNavigationViewerProvider()) { Query = "   " };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Null(service.LastRequest);
    Assert.Empty(viewModel.Results);
  }

  [Fact]
  public async Task StaleResponseIsDiscardedInFavorOfNewerQuery()
  {
    var service = new Service { Pending = new TaskCompletionSource<UsersSearchResponse>() };
    var viewModel = new UsersBrowseViewModel(service, new MutableNavigationViewerProvider()) { Query = "alice" };
    var pending = viewModel.SearchAsync(TestContext.Current.CancellationToken);
    viewModel.Query = "bob";
    service.Pending.SetResult(new UsersSearchResponse([new("user-1", "alice")], new PageInfo(null, false, null)));
    await pending;

    Assert.Empty(viewModel.Results);
    Assert.False(viewModel.IsSearching);
  }

  [Fact]
  public async Task FourHundredLevelApiFailureSurfacesServerMessage()
  {
    var service = new Service
    {
      Error = new VouchaApiException(HttpStatusCode.UnprocessableEntity, "{\"error\":{\"message\":\"Try a shorter query\"}}"),
    };
    var viewModel = new UsersBrowseViewModel(service, new MutableNavigationViewerProvider()) { Query = "alice" };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Null(viewModel.SearchError!.Value.Key);
    Assert.Equal("Try a shorter query", UiLocalization.English.Resolve(viewModel.SearchError.Value));
  }

  [Fact]
  public async Task TransportFailureFallsBackToLocalizedMessage()
  {
    var service = new Service { Error = new HttpRequestException("offline") };
    var viewModel = new UsersBrowseViewModel(service, new MutableNavigationViewerProvider()) { Query = "alice" };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Equal(UiMessageKey.NativeSwiftMembershipSearchFailure, viewModel.SearchError!.Value.Key);
  }

  [Fact]
  public async Task SearchRestoresContinuationStateForLoadMore()
  {
    var service = new Service
    {
      Response = new UsersSearchResponse([new UserSearchResult("user-1", "alice")], new PageInfo("cursor-1", true, null)),
    };
    var viewModel = new UsersBrowseViewModel(service, new MutableNavigationViewerProvider()) { Query = "alice" };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasMoreResults);
    Assert.False(viewModel.IsLoadingMoreResults);
    Assert.False(viewModel.HasMoreResultsError);
  }

  [Fact]
  public async Task LoadMoreForwardsEndCursorAndAppendsDedupedResults()
  {
    var service = new Service
    {
      Response = new UsersSearchResponse([new UserSearchResult("user-1", "alice")], new PageInfo("cursor-1", true, null)),
    };
    var viewModel = new UsersBrowseViewModel(service, new MutableNavigationViewerProvider()) { Query = "alice" };
    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    service.Response = new UsersSearchResponse(
        [new UserSearchResult("user-1", "alice"), new UserSearchResult("user-2", "alicia")],
        new PageInfo(null, false, null));
    await viewModel.LoadMoreResultsAsync(TestContext.Current.CancellationToken);

    Assert.Equal("cursor-1", service.LastRequest!.After);
    Assert.Equal("alice", service.LastRequest!.Query);
    Assert.Equal(2, viewModel.Results.Count);
    Assert.False(viewModel.HasMoreResults);
  }

  [Fact]
  public async Task LoadMoreFailureSurfacesPaginationErrorWithoutClearingExistingResults()
  {
    var service = new Service
    {
      Response = new UsersSearchResponse([new UserSearchResult("user-1", "alice")], new PageInfo("cursor-1", true, null)),
    };
    var viewModel = new UsersBrowseViewModel(service, new MutableNavigationViewerProvider()) { Query = "alice" };
    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    service.Error = new HttpRequestException("offline");
    await viewModel.LoadMoreResultsAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasMoreResultsError);
    Assert.False(viewModel.IsLoadingMoreResults);
    Assert.Single(viewModel.Results);
    Assert.True(viewModel.HasMoreResults);
  }

  [Fact]
  public async Task LoadMoreWithoutAnyLoadedPageIssuesNoRequest()
  {
    var service = new Service();
    var viewModel = new UsersBrowseViewModel(service, new MutableNavigationViewerProvider());

    await viewModel.LoadMoreResultsAsync(TestContext.Current.CancellationToken);

    Assert.Null(service.LastRequest);
  }

  [Theory]
  [InlineData(new[] { "administrator" }, true)]
  public void IsAdministratorReflectsAdministratorRoleOnly(string[] roles, bool expected)
  {
    var viewerProvider = new MutableNavigationViewerProvider();
    viewerProvider.SetViewer(new NavigationViewer(true, roles));
    var viewModel = new UsersBrowseViewModel(new Service(), viewerProvider);

    Assert.Equal(expected, viewModel.IsAdministrator);
  }

  [Fact]
  public void AnonymousViewerIsNotAdministrator()
  {
    var viewerProvider = new MutableNavigationViewerProvider();
    viewerProvider.SetViewer(NavigationViewer.Anonymous);
    var viewModel = new UsersBrowseViewModel(new Service(), viewerProvider);

    Assert.False(viewModel.IsAdministrator);
  }

  private sealed class Service : IUsersSearchService
  {
    public SearchUsersRequest? LastRequest { get; private set; }
    public TaskCompletionSource<UsersSearchResponse>? Pending { get; init; }
    public Exception? Error { get; set; }
    public UsersSearchResponse Response { get; set; } =
        new([new UserSearchResult("user-1", "alice")], new PageInfo(null, false, null));

    public Task<UsersSearchResponse> SearchUsersAsync(SearchUsersRequest request, CancellationToken cancellationToken = default)
    {
      LastRequest = request;
      if (Pending is not null) return Pending.Task;
      return Error is null
          ? Task.FromResult(Response)
          : Task.FromException<UsersSearchResponse>(Error);
    }
  }
}
