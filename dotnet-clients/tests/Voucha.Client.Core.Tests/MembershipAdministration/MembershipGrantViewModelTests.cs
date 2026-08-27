using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.MembershipAdministration;
using Xunit;

namespace Voucha.Client.Core.Tests.MembershipAdministration;

public sealed class MembershipGrantViewModelTests
{
  [Fact]
  public async Task GrantUsesSelectedUserAndClearsFormAfterSuccess()
  {
    var service = new Service();
    var viewModel = new MembershipGrantViewModel(service);
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    viewModel.SelectUser(User());
    viewModel.SelectedPlan = MembershipGrantPlanSlug.Plus;
    viewModel.SelectedSku = viewModel.AvailableSkus.Single();
    Assert.True(await viewModel.GrantAsync(TestContext.Current.CancellationToken));
    Assert.Equal("user-1", service.Grant!.UserId);
    Assert.Null(viewModel.SelectedUser);
    Assert.NotNull(viewModel.Success);
  }

  [Fact]
  public async Task UsernameLessUserKeepsStableIdentifierThroughGrant()
  {
    var service = new Service();
    var viewModel = new MembershipGrantViewModel(service);
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    viewModel.SelectUser(new UserSearchResult("user-without-username", null));
    Assert.Equal("user-without-username", viewModel.Query);
    viewModel.SelectedPlan = MembershipGrantPlanSlug.Plus;
    viewModel.SelectedSku = viewModel.AvailableSkus.Single();

    Assert.True(await viewModel.GrantAsync(TestContext.Current.CancellationToken));
    Assert.Equal("user-without-username", service.Grant!.UserId);
  }

  [Fact]
  public async Task EditedQueryInvalidatesDelayedSearchResults()
  {
    var service = new Service { Search = new TaskCompletionSource<UsersSearchResponse>() };
    var viewModel = new MembershipGrantViewModel(service) { Query = "alice" };
    var pending = viewModel.SearchAsync(TestContext.Current.CancellationToken);
    viewModel.Query = "bob";
    service.Search.SetResult(new UsersSearchResponse([User()], new PageInfo(null, false, null)));
    await pending;
    Assert.Empty(viewModel.Results);
    Assert.False(viewModel.IsSearching);
  }

  [Fact]
  public async Task FailedGrantPreservesSelections()
  {
    var service = new Service { GrantError = new InvalidOperationException("denied") };
    var viewModel = new MembershipGrantViewModel(service);
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    viewModel.SelectUser(User()); viewModel.SelectedPlan = MembershipGrantPlanSlug.Plus; viewModel.SelectedSku = viewModel.AvailableSkus.Single();
    Assert.False(await viewModel.GrantAsync(TestContext.Current.CancellationToken));
    Assert.NotNull(viewModel.SelectedUser); Assert.NotNull(viewModel.SelectedSku); Assert.NotNull(viewModel.Error);
  }

  [Fact]
  public async Task SelectionSuppressesDelayedSearchAndPlansCacheAfterSuccess()
  {
    var service = new Service { Search = new TaskCompletionSource<UsersSearchResponse>() };
    var viewModel = new MembershipGrantViewModel(service) { Query = "alice" };
    var search = viewModel.SearchAsync(TestContext.Current.CancellationToken);
    viewModel.SelectUser(User());
    service.Search.SetResult(new UsersSearchResponse([new("stale", "stale")], new PageInfo(null, false, null)));
    await search;
    Assert.Empty(viewModel.Results);
    Assert.False(viewModel.IsSearching);
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    viewModel.SelectedPlan = MembershipGrantPlanSlug.Plus; viewModel.SelectedSku = viewModel.AvailableSkus.Single();
    Assert.True(await viewModel.GrantAsync(TestContext.Current.CancellationToken));
    Assert.Equal(2, service.PlanCalls);
  }

  [Fact]
  public async Task GrantRefreshesPlansAndRejectsARetiredSku()
  {
    var service = new Service();
    var viewModel = new MembershipGrantViewModel(service);
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    viewModel.SelectUser(User());
    viewModel.SelectedPlan = MembershipGrantPlanSlug.Plus;
    viewModel.SelectedSku = viewModel.AvailableSkus.Single();
    service.Plans = new Dictionary<string, IReadOnlyList<MembershipSku>>();

    Assert.False(await viewModel.GrantAsync(TestContext.Current.CancellationToken));
    Assert.Null(service.Grant);
    Assert.Null(viewModel.SelectedSku);
    Assert.False(viewModel.CanSubmit);
    Assert.Equal(2, service.PlanCalls);
  }

  [Fact]
  public async Task GrantUsesTheSelectionCapturedBeforeItsPlanRefresh()
  {
    var refresh = new TaskCompletionSource<MembershipPlansResponse>();
    var service = new Service { Plans = Plans(), PendingRefresh = refresh };
    var viewModel = new MembershipGrantViewModel(service);
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    viewModel.SelectUser(User());
    viewModel.SelectedPlan = MembershipGrantPlanSlug.Plus;
    viewModel.SelectedSku = viewModel.AvailableSkus.Single();

    var granting = viewModel.GrantAsync(TestContext.Current.CancellationToken);
    viewModel.SelectUser(new UserSearchResult("user-2", "bob"));
    viewModel.SelectedPlan = MembershipGrantPlanSlug.Pro;
    viewModel.SelectedSku = viewModel.AvailableSkus.Single();
    refresh.SetResult(new MembershipPlansResponse(Plans()));

    Assert.True(await granting);
    Assert.Equal(new GrantMembershipBody("user-1", MembershipGrantPlanSlug.Plus, "sku-plus"), service.Grant);
  }

  [Fact]
  public async Task FailedPlanRefreshClearsSubmittingState()
  {
    var service = new Service();
    var viewModel = new MembershipGrantViewModel(service);
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    viewModel.SelectUser(User());
    viewModel.SelectedPlan = MembershipGrantPlanSlug.Plus;
    viewModel.SelectedSku = viewModel.AvailableSkus.Single();
    service.PlanError = new HttpRequestException("offline");

    Assert.False(await viewModel.GrantAsync(TestContext.Current.CancellationToken));
    Assert.False(viewModel.IsSubmitting);
    Assert.Null(service.Grant);
    Assert.NotNull(viewModel.PlanError);
    Assert.Null(viewModel.SearchError);

    service.PlanError = null;
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    Assert.Null(viewModel.PlanError);
    Assert.Equal(3, service.PlanCalls);
  }

  [Fact]
  public async Task StaleSearchFailureDoesNotReplaceNewerQueryState()
  {
    var service = new Service { Search = new TaskCompletionSource<UsersSearchResponse>() };
    var viewModel = new MembershipGrantViewModel(service) { Query = "alice" };
    var search = viewModel.SearchAsync(TestContext.Current.CancellationToken);
    viewModel.Query = "bob";
    service.Search.SetException(new HttpRequestException("offline"));
    await search;
    Assert.Equal("bob", viewModel.Query);
    Assert.Null(viewModel.Error);
    Assert.False(viewModel.IsSearching);
  }

  [Fact]
  public async Task PlanChangeClearsSkuAndFiltersMismatchedSku()
  {
    var service = new Service
    {
      Plans = new Dictionary<string, IReadOnlyList<MembershipSku>>
      {
        ["plus"] = [new("ok", "plus", new Money(500, "usd"), "monthly", "price"), new("wrong", "pro", new Money(500, "usd"), "monthly", "price")],
      }
    };
    var viewModel = new MembershipGrantViewModel(service);
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    viewModel.SelectedPlan = MembershipGrantPlanSlug.Plus; viewModel.SelectedSku = viewModel.AvailableSkus.Single();
    viewModel.SelectedPlan = MembershipGrantPlanSlug.Pro;
    Assert.Null(viewModel.SelectedSku); Assert.Empty(viewModel.AvailableSkus);
  }

  [Fact]
  public async Task FailureCanRetryAndClassifiesServerDetail()
  {
    var service = new Service { PlanError = new HttpRequestException("offline") };
    var viewModel = new MembershipGrantViewModel(service);
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(viewModel.Error!.Value.Key);
    service.PlanError = null;
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, service.PlanCalls);
  }

  [Fact]
  public async Task ApiFailuresExposeOnlyIntentionalFourHundredMessages()
  {
    var clientFailure = new MembershipGrantViewModel(new Service
    {
      PlanError = new VouchaApiException(
          HttpStatusCode.UnprocessableEntity,
          "{\"error\":{\"message\":\"Choose another SKU\"}}"),
    });
    await clientFailure.LoadPlansAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Choose another SKU", clientFailure.LocalizedError);
    Assert.Null(clientFailure.PlanError!.Value.Key);

    var serverFailure = new MembershipGrantViewModel(new Service
    {
      PlanError = new VouchaApiException(
          HttpStatusCode.InternalServerError,
          "{\"message\":\"Internal implementation detail\"}"),
    });
    await serverFailure.LoadPlansAsync(TestContext.Current.CancellationToken);
    Assert.Equal(UiMessageKey.NativeSwiftMembershipPlansLoadFailure, serverFailure.PlanError!.Value.Key);
    Assert.DoesNotContain("HTTP", serverFailure.LocalizedError!, StringComparison.Ordinal);

    var blankClientFailure = new MembershipGrantViewModel(new Service
    {
      PlanError = new VouchaApiException(HttpStatusCode.BadRequest, "{\"message\":\"   \"}"),
    });
    await blankClientFailure.LoadPlansAsync(TestContext.Current.CancellationToken);
    Assert.Equal(UiMessageKey.NativeSwiftMembershipPlansLoadFailure, blankClientFailure.PlanError!.Value.Key);
  }

  [Fact]
  public async Task NewInvalidGrantClearsPreviousSuccess()
  {
    var viewModel = new MembershipGrantViewModel(new Service());
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    viewModel.SelectUser(User());
    viewModel.SelectedPlan = MembershipGrantPlanSlug.Plus;
    viewModel.SelectedSku = viewModel.AvailableSkus.Single();
    Assert.True(await viewModel.GrantAsync(TestContext.Current.CancellationToken));
    Assert.NotNull(viewModel.Success);

    Assert.False(await viewModel.GrantAsync(TestContext.Current.CancellationToken));
    Assert.Null(viewModel.Success);
    Assert.Equal(UiMessageKey.NativeSwiftMembershipMembershipGrantValidation, viewModel.SearchError!.Value.Key);
  }

  [Fact]
  public async Task SearchFollowsCursorToFillPage()
  {
    var service = new Service
    {
      SearchResponses = new Queue<UsersSearchResponse>(
      [
        new UsersSearchResponse(
            [.. Enumerable.Range(0, 6).Select(index => new UserSearchResult($"u{index}", $"al{index}"))],
            new PageInfo("cursor-1", true, null)),
        new UsersSearchResponse(
            [.. Enumerable.Range(6, 4).Select(index => new UserSearchResult($"u{index}", $"al{index}"))],
            new PageInfo(null, false, null)),
      ]),
    };
    var viewModel = new MembershipGrantViewModel(service) { Query = "alice" };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Equal(10, viewModel.Results.Count);
    Assert.Equal(2, service.SearchRequests.Count);
    Assert.Null(service.SearchRequests[0].After);
    Assert.Equal("cursor-1", service.SearchRequests[1].After);
  }

  [Fact]
  public async Task SearchStopsAtPageSafetyBoundWithoutFillingPage()
  {
    var service = new Service
    {
      SearchResponses = new Queue<UsersSearchResponse>(Enumerable.Range(0, 5).Select(index =>
          new UsersSearchResponse(
              [new UserSearchResult($"u{index}", $"al{index}")],
              new PageInfo($"cursor-{index}", true, null)))),
    };
    var viewModel = new MembershipGrantViewModel(service) { Query = "alice" };

    await viewModel.SearchAsync(TestContext.Current.CancellationToken);

    Assert.Equal(5, viewModel.Results.Count);
    Assert.Equal(5, service.SearchRequests.Count);
  }

  [Fact]
  public async Task SearchFailureDoesNotClearPlanLoadFailure()
  {
    var service = new Service { PlanError = new HttpRequestException("offline"), SearchError = new HttpRequestException("offline") };
    var viewModel = new MembershipGrantViewModel(service) { Query = "alice" };
    await viewModel.LoadPlansAsync(TestContext.Current.CancellationToken);
    var planError = viewModel.PlanError;
    await viewModel.SearchAsync(TestContext.Current.CancellationToken);
    Assert.Equal(planError, viewModel.PlanError);
    Assert.NotNull(viewModel.SearchError);
    Assert.Equal(viewModel.SearchError, viewModel.Error);
  }

  private static UserSearchResult User() => new("user-1", "alice");

  private static IReadOnlyDictionary<string, IReadOnlyList<MembershipSku>> Plans() =>
      new Dictionary<string, IReadOnlyList<MembershipSku>>
      {
        ["plus"] = [new("sku-plus", "plus", new Money(500, "usd"), "monthly", "price")],
        ["pro"] = [new("sku-pro", "pro", new Money(1_000, "usd"), "monthly", "price")],
      };

  private sealed class Service : IMembershipAdministrationService
  {
    public GrantMembershipBody? Grant { get; private set; }
    public Exception? GrantError { get; init; }
    public Exception? PlanError { get; set; }
    public int PlanCalls { get; private set; }
    public IReadOnlyDictionary<string, IReadOnlyList<MembershipSku>>? Plans { get; set; }
    public TaskCompletionSource<MembershipPlansResponse>? PendingRefresh { get; init; }
    public TaskCompletionSource<UsersSearchResponse>? Search { get; init; }
    public Exception? SearchError { get; init; }
    public Queue<UsersSearchResponse>? SearchResponses { get; init; }
    public List<SearchUsersRequest> SearchRequests { get; } = [];
    public Task<MembershipPlansResponse> FetchPlansAsync(CancellationToken cancellationToken = default)
    {
      PlanCalls++;
      if (PlanCalls > 1 && PendingRefresh is not null) return PendingRefresh.Task;
      return PlanError is null ? Task.FromResult(new MembershipPlansResponse(Plans ?? new Dictionary<string, IReadOnlyList<MembershipSku>> { ["plus"] = [new("sku-1", "plus", new Money(500, "usd"), "monthly", "price")] })) : Task.FromException<MembershipPlansResponse>(PlanError);
    }
    public Task<UsersSearchResponse> SearchUsersAsync(SearchUsersRequest request, CancellationToken cancellationToken = default)
    {
      SearchRequests.Add(request);
      if (SearchResponses is { Count: > 0 } queue) return Task.FromResult(queue.Dequeue());
      return Search?.Task ?? (SearchError is null
          ? Task.FromResult(new UsersSearchResponse([], new PageInfo(null, false, null)))
          : Task.FromException<UsersSearchResponse>(SearchError));
    }
    public Task<GrantMembershipResponse> GrantAsync(GrantMembershipBody body, CancellationToken cancellationToken = default)
    {
      Grant = body; return GrantError is null ? Task.FromResult(new GrantMembershipResponse(new MembershipGrantResult("membership-1"))) : Task.FromException<GrantMembershipResponse>(GrantError);
    }
  }
}
