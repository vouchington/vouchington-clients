using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Engineering;

public sealed class AgentListsViewModelTests
{
  [Fact]
  public void PaginationActionTitleRefreshesWithLocale()
  {
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    using var viewModel = new AgentListsViewModel(
        new StubService(),
        new UiLocalization(controller),
        controller);
    var changes = 0;
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName == nameof(AgentListsViewModel.PaginationActionTitle)) changes++;
    };

    Assert.Equal("Load more", viewModel.PaginationActionTitle);

    controller.ApplySavedLocale("es");

    Assert.Equal("Cargar más", viewModel.PaginationActionTitle);
    Assert.Equal(1, changes);
  }

  [Theory]
  [InlineData(true, false)]
  [InlineData(false, true)]
  public async Task InitialLoadFailuresUseLocalizedErrorAndRefreshWithLocale(
      bool failsDetailEndpoint,
      bool failsConversationsEndpoint)
  {
    var service = new StubService
    {
      Detail = failsDetailEndpoint
          ? Task.FromException<AgentDetailResponse>(new HttpRequestException("detail unavailable"))
          : Task.FromResult(new AgentDetailResponse(Agent("detail"), null)),
      ConversationPages = new([
        failsConversationsEndpoint
            ? Task.FromException<AgentConversationsResponse>(new HttpRequestException("conversations unavailable"))
            : Task.FromResult(Conversations([], false, null)),
      ]),
    };
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    using var viewModel = new AgentListsViewModel(service, localization, controller);
    var refreshedProperties = new List<string?>();
    viewModel.PropertyChanged += (_, args) => refreshedProperties.Add(args.PropertyName);

    await viewModel.LoadConversationsAsync("helper", TestContext.Current.CancellationToken);
    Assert.Equal(localization.Localize(UiMessageKey.NativeDotnetCsharpError), viewModel.ErrorMessage);
    Assert.DoesNotContain("unavailable", viewModel.ErrorMessage!, StringComparison.Ordinal);

    controller.ApplySavedLocale("pt");
    Assert.Contains(nameof(AgentListsViewModel.ErrorMessage), refreshedProperties);
    Assert.Equal(localization.Localize(UiMessageKey.NativeDotnetCsharpError), viewModel.ErrorMessage);
  }

  [Fact]
  public async Task DirectoryInitialFailureUsesLocalizedError()
  {
    var service = new StubService { AgentPages = new([Task.FromException<AgentsResponse>(new HttpRequestException("agents unavailable"))]) };
    var viewModel = new AgentListsViewModel(service);

    await viewModel.LoadAgentsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpError), viewModel.ErrorMessage);
  }

  [Fact]
  public void AgentStatusIsAbsentUntilDetailLoads()
  {
    var viewModel = new AgentListsViewModel(new StubService());

    Assert.Null(viewModel.AgentIsActive);
    Assert.False(viewModel.HasAgentStatus);
    Assert.Null(viewModel.AgentStatusTitle);
  }

  [Fact]
  public async Task AgentContinuationIsOneFlightAndPreservesCurrentOverlap()
  {
    var service = new StubService
    {
      AgentPages = new([
        Task.FromResult(Agents([Agent("current")], true, "next")),
        Task.FromResult(Agents([Agent("older"), Agent("current", "stale")], false, null)),
      ]),
    };
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    using var viewModel = new AgentListsViewModel(service, new UiLocalization(controller), controller);

    await viewModel.LoadAgentsAsync(TestContext.Current.CancellationToken);
    await Task.WhenAll(
        viewModel.LoadMoreAsync(TestContext.Current.CancellationToken),
        viewModel.LoadMoreAsync(TestContext.Current.CancellationToken));

    Assert.Equal(["current", "older"], viewModel.Rows.Select(item => item.Id));
    Assert.Equal("user-cur", viewModel.Rows[0].Title);
    Assert.StartsWith("helper · Inactive ·", viewModel.Rows[0].Detail);
    Assert.Equal(2, service.AgentRequests.Count);
    Assert.Equal("next", service.AgentRequests[1].After);
  }

  [Fact]
  public async Task AgentPagesMergeUsersAndPresentAccountNameOrIdPrefix()
  {
    var service = new StubService
    {
      AgentPages = new([
        Task.FromResult(Agents([Agent("first")], true, "next", new Dictionary<string, PublicUser>
        {
          ["user-first"] = new("user-first", "first-user", DisplayAccount: new UserDisplayAccount("account", "First account")),
        })),
        Task.FromResult(Agents([Agent("second"), Agent("missing")], false, null, new Dictionary<string, PublicUser>
        {
          ["user-second"] = new("user-second", "second-user"),
        })),
      ]),
    };
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    using var viewModel = new AgentListsViewModel(service, new UiLocalization(controller), controller);

    await viewModel.LoadAgentsAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["First account", "second-user", "user-mis"], viewModel.Rows.Select(row => row.Title));
    Assert.All(viewModel.Rows, row => Assert.Contains("Inactive", row.Detail));
    var englishDetail = viewModel.Rows[0].Detail;
    controller.ApplySavedLocale("es");
    Assert.Contains("Inactivo", viewModel.Rows[0].Detail);
    Assert.NotEqual(englishDetail, viewModel.Rows[0].Detail);
    var spanishDetail = viewModel.Rows[0].Detail;
    controller.ApplySavedLocale("en");
    Assert.Equal(englishDetail, viewModel.Rows[0].Detail);
    Assert.NotEqual(spanishDetail, viewModel.Rows[0].Detail);
  }

  [Fact]
  public async Task AgentDirectoryRowsDeriveActiveStatusFromActivationAndDeactivation()
  {
    var service = new StubService
    {
      AgentPages = new([Task.FromResult(Agents(
          [
            Agent("active") with { ActivatedAt = DateTimeOffset.UnixEpoch },
            Agent("inactive"),
            Agent("deactivated") with
            {
              ActivatedAt = DateTimeOffset.UnixEpoch,
              DeactivatedAt = DateTimeOffset.UnixEpoch,
            },
          ],
          false,
          null))]),
    };
    var viewModel = new AgentListsViewModel(service);

    await viewModel.LoadAgentsAsync(TestContext.Current.CancellationToken);

    Assert.Contains("Active", viewModel.Rows.Single(row => row.Id == "active").Detail);
    Assert.Contains("Inactive", viewModel.Rows.Single(row => row.Id == "inactive").Detail);
    Assert.Contains("Inactive", viewModel.Rows.Single(row => row.Id == "deactivated").Detail);
  }

  [Fact]
  public async Task ConversationContinuationFailurePreservesRowsAndRetries()
  {
    var service = new StubService
    {
      ConversationPages = new([
        Task.FromResult(Conversations([Conversation("current")], true, "next")),
        Task.FromException<AgentConversationsResponse>(new HttpRequestException("offline")),
        Task.FromResult(Conversations([Conversation("older")], false, null)),
      ]),
    };
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    using var viewModel = new AgentListsViewModel(service, new UiLocalization(controller), controller);

    await viewModel.LoadConversationsAsync("helper", TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["current"], viewModel.Rows.Select(item => item.Id));
    Assert.Equal("Error", viewModel.PaginationErrorMessage);
    Assert.Equal("Retry", viewModel.PaginationActionTitle);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["current", "older"], viewModel.Rows.Select(item => item.Id));
    Assert.Null(viewModel.PaginationErrorMessage);
  }

  [Theory]
  [InlineData("en", "", "Conversation title")]
  [InlineData("es", "   ", "Título de la conversación")]
  public async Task UntitledConversationUsesLocalizedFallback(string language, string title, string expected)
  {
    var service = new StubService
    {
      ConversationPages = new([Task.FromResult(Conversations([Conversation("untitled", title)], false, null))]),
    };
    using var controller = new UiLocaleController(new StubLanguageProvider(language));
    using var viewModel = new AgentListsViewModel(service, new UiLocalization(controller), controller);

    await viewModel.LoadConversationsAsync("helper", TestContext.Current.CancellationToken);

    Assert.Equal(expected, Assert.Single(viewModel.Rows).Title);
  }

  [Fact]
  public async Task ConversationRowsResolveCreatorDisplayNamesAndIdFallback()
  {
    var createdAt = new DateTimeOffset(2026, 7, 1, 16, 0, 0, TimeSpan.Zero);
    var service = new StubService
    {
      ConversationPages = new([Task.FromResult(Conversations(
          [
            Conversation("first", createdById: "support-one", createdAt: createdAt),
            Conversation("second", createdById: "missing-user"),
            Conversation("deleted", createdById: null),
          ],
          false,
          null,
          new Dictionary<string, PublicUser>
          {
            ["support-one"] = new("support-one", "support-one", DisplayAccount: new UserDisplayAccount("account", "Support One")),
          }))]),
    };
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    using var viewModel = new AgentListsViewModel(service, new UiLocalization(controller), controller);

    await viewModel.LoadConversationsAsync("helper", TestContext.Current.CancellationToken);

    Assert.StartsWith("Support One ·", viewModel.Rows[0].Detail);
    Assert.StartsWith("missing- ·", viewModel.Rows[1].Detail);
    Assert.StartsWith("Deleted ·", viewModel.Rows[2].Detail);
    var englishDetail = viewModel.Rows[0].Detail;
    controller.ApplySavedLocale("es");
    var spanishLocalization = new UiLocalization(controller);
    Assert.Equal(
        $"Support One · {TimeZoneInfo.ConvertTime(createdAt, TimeZoneInfo.Local).ToString("d", spanishLocalization.Culture)}",
        viewModel.Rows[0].Detail);
    Assert.NotEqual(englishDetail, viewModel.Rows[0].Detail);
  }

  [Theory]
  [InlineData(AgentConversationFilterKind.UserId, "user_id")]
  [InlineData(AgentConversationFilterKind.Username, "username")]
  [InlineData(AgentConversationFilterKind.PostId, "post_id")]
  [InlineData(AgentConversationFilterKind.PostSlug, "post_slug")]
  [InlineData(AgentConversationFilterKind.RssFeedItemId, "rss_feed_item_id")]
  public async Task HydratedConversationFilterScopesInitialDetailLoad(
      AgentConversationFilterKind kind,
      string queryName)
  {
    var filter = AgentConversationFilter.FromQuery(new Dictionary<string, string> { [queryName] = "  target  " });
    var service = new StubService
    {
      ConversationPages = new([Task.FromResult(Conversations([], false, null))]),
    };
    var viewModel = new AgentListsViewModel(service);

    viewModel.HydrateConversationFilter(filter);
    await viewModel.LoadConversationsAsync("helper", TestContext.Current.CancellationToken);

    Assert.Equal(new AgentConversationFilter(kind, "target"), Assert.Single(service.ConversationRequests).Filter);
  }

  [Fact]
  public void HydratedConversationFilterUsesBackendQueryPrecedence()
  {
    var filter = AgentConversationFilter.FromQuery(new Dictionary<string, string>
    {
      ["post_slug"] = "post",
      ["user_id"] = "user",
      ["username"] = "name",
    });

    Assert.Equal(new AgentConversationFilter(AgentConversationFilterKind.UserId, "user"), filter);
  }

  [Fact]
  public async Task UntitledConversationFallbackRefreshesWithLocale()
  {
    var service = new StubService
    {
      ConversationPages = new([Task.FromResult(Conversations([Conversation("untitled", "")], false, null))]),
    };
    using var controller = new UiLocaleController(new StubLanguageProvider("en"));
    using var viewModel = new AgentListsViewModel(service, new UiLocalization(controller), controller);
    await viewModel.LoadConversationsAsync("helper", TestContext.Current.CancellationToken);

    controller.ApplySavedLocale("es");

    Assert.Equal("Título de la conversación", Assert.Single(viewModel.Rows).Title);
  }

  [Fact]
  public async Task DetailAndFirstConversationPageCommitTogetherAndFilterIdentityRejectsStaleResponse()
  {
    var first = new TaskCompletionSource<AgentConversationsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var filtered = new TaskCompletionSource<AgentConversationsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new StubService
    {
      Detail = Task.FromResult(new AgentDetailResponse(Agent("detail") with { Slug = "helper" }, null)),
      ConversationPages = new([first.Task, filtered.Task]),
    };
    var viewModel = new AgentListsViewModel(service);
    var initial = viewModel.LoadConversationsAsync("helper", TestContext.Current.CancellationToken);
    await Task.Yield();
    Assert.Null(viewModel.AgentDetail);
    var search = viewModel.SearchConversationsAsync(AgentConversationFilterKind.Username, "fixture", TestContext.Current.CancellationToken);
    first.SetResult(Conversations([Conversation("stale")], false, null));
    filtered.SetResult(Conversations([Conversation("filtered")], false, null));
    await Task.WhenAll(initial, search);

    Assert.Equal("detail", viewModel.AgentDetail?.Agent.Id);
    Assert.Equal(["filtered"], viewModel.Rows.Select(row => row.Id));
    Assert.Equal(AgentConversationFilterKind.Username, service.ConversationRequests[1].Filter?.Kind);
  }

  private static AgentsResponse Agents(
      IReadOnlyList<AgentSummary> rows,
      bool more,
      string? cursor,
      IReadOnlyDictionary<string, PublicUser>? users = null) =>
      new(rows, new PageInfo(cursor, more, rows.FirstOrDefault()?.Id), users ?? new Dictionary<string, PublicUser>());

  private static AgentSummary Agent(string id, string type = "helper") =>
      new(id, $"user-{id}", type, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);

  private static AgentConversationsResponse Conversations(
      IReadOnlyList<AgentConversationSummary> rows,
      bool more,
      string? cursor,
      IReadOnlyDictionary<string, PublicUser>? users = null) =>
      new(rows, new PageInfo(cursor, more, rows.FirstOrDefault()?.Id), users ?? new Dictionary<string, PublicUser>());

  private static AgentConversationSummary Conversation(
      string id,
      string? title = null,
      string? createdById = "user",
      DateTimeOffset? createdAt = null) =>
      new(
          id,
          title ?? id,
          createdAt ?? DateTimeOffset.UnixEpoch,
          createdById,
          createdAt ?? DateTimeOffset.UnixEpoch,
          null,
          null,
          null);

  private sealed class StubService : IAgentConversationsService
  {
    public Queue<Task<AgentsResponse>> AgentPages { get; init; } = [];
    public Queue<Task<AgentConversationsResponse>> ConversationPages { get; init; } = [];
    public List<FetchAgentsRequest> AgentRequests { get; } = [];
    public List<FetchAgentConversationsRequest> ConversationRequests { get; } = [];
    public Task<AgentDetailResponse> Detail { get; init; } = Task.FromResult(
        new AgentDetailResponse(Agent("detail"), null));

    public Task<AgentsResponse> FetchAgentsAsync(FetchAgentsRequest request, CancellationToken cancellationToken = default)
    {
      AgentRequests.Add(request);
      return AgentPages.Dequeue();
    }

    public Task<AgentConversationsResponse> FetchAgentConversationsAsync(
        FetchAgentConversationsRequest request, CancellationToken cancellationToken = default)
    {
      ConversationRequests.Add(request);
      return ConversationPages.Dequeue();
    }

    public Task<AgentDetailResponse> FetchAgentAsync(string idOrSlug, CancellationToken cancellationToken = default) => Detail;

    public Task<AgentConversationDetailResponse> FetchAgentConversationAsync(
        FetchAgentConversationRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }

  private sealed class StubLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}
