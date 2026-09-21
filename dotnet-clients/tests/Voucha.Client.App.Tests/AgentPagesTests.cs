using System.Collections;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class AgentPagesTests
{
  [Fact]
  public async Task ListRendersLoadingInitialErrorRetryAndTargetTap()
  {
    var service = new Service();
    var pending = new TaskCompletionSource<AgentsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentResponses.Enqueue(pending.Task);
    var viewModel = new AgentListsViewModel(service);
    var page = ListPage(viewModel);
    var load = page.ApplyRouteAsync();
    await WaitUntilAsync(() => viewModel.IsLoading);
    Assert.True(Find<ActivityIndicator>(page, "agent-loading").IsVisible);

    pending.SetException(new InvalidOperationException("agents unavailable"));
    await load;
    Assert.Contains(Labels(page), label => label == UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpError));
    Assert.True(Find<Button>(page, "agent-retry").IsVisible);

    service.AgentResponses.Enqueue(Task.FromResult(Agents([Agent("agent/one")])));
    Find<Button>(page, "agent-retry").SendClicked();
    await WaitUntilAsync(() => viewModel.Rows.Count == 1);
    Assert.False(Find<Button>(page, "agent-retry").IsVisible);

    NativeRoutePath? target = null;
    page.SetNavigator(path => { target = path; return Task.CompletedTask; });
    Assert.Single(Descendants<CollectionView>(page)).SelectedItem = viewModel.Rows.Single();
    Assert.Equal("/agent/agent%2Fone", target?.Value);

    page.SetInitialTarget(AgentNavigationTargets.Conversation("agent/one", "conversation:one"));
    Assert.True(await page.OpenInitialTargetAsync());
    Assert.Equal("/agent/agent%2Fone/conversation/conversation%3Aone", target?.Value);
    Assert.False(await page.OpenInitialTargetAsync());
  }

  [Fact]
  public async Task DetailRendersMetadataInitialErrorRetryAndAllFilterActions()
  {
    var service = new Service();
    service.DetailResponses.Enqueue(Task.FromException<AgentDetailResponse>(new InvalidOperationException("detail unavailable")));
    var viewModel = new AgentListsViewModel(service);
    var page = DetailPage(viewModel);
    await page.ApplyRouteAsync("agent");
    Assert.Contains(Labels(page), label => label == UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpError));
    Assert.True(Find<Button>(page, "agent-retry").IsVisible);

    service.DetailResponses.Enqueue(Task.FromResult(Detail(active: true, username: "account-fallback")));
    Find<Button>(page, "agent-retry").SendClicked();
    await WaitUntilAsync(() => viewModel.AgentDetail is not null);
    Assert.Contains(Labels(page), label => label == "account-fallback");
    Assert.Contains(Labels(page), label => label == "Active");
    Assert.Contains(Labels(page), label => label == "Created");
    Assert.Contains(Labels(page), label => label == UiLocalization.English.FormatDateTime(DateTimeOffset.UnixEpoch, TimeZoneInfo.Local));

    service.DetailResponses.Enqueue(Task.FromResult(Detail(active: false, username: null)));
    await page.ApplyRouteAsync("agent");
    Assert.Contains(Labels(page), label => label == "Inactive");
    Assert.Contains(Labels(page), label => label == "system");

    var kind = Find<Picker>(page, "agent-filter-kind");
    var value = Find<Entry>(page, "agent-filter-value");
    Assert.Equal(5, ((IEnumerable)kind.ItemsSource!).Cast<string>().Count());
    foreach (var filterKind in Enum.GetValues<AgentConversationFilterKind>())
    {
      kind.SelectedIndex = (int)filterKind;
      value.Text = " term ";
      Find<Button>(page, "agent-filter-search").SendClicked();
      await WaitUntilAsync(() => service.ConversationRequests.LastOrDefault()?.Filter?.Kind == filterKind);
      Assert.Equal("term", service.ConversationRequests.Last().Filter?.Value);
    }
    Find<Button>(page, "agent-filter-clear").SendClicked();
    await WaitUntilAsync(() => service.ConversationRequests.LastOrDefault()?.Filter is null);
    Assert.Equal(string.Empty, value.Text);

    value.Text = "old-agent-filter";
    kind.SelectedIndex = (int)AgentConversationFilterKind.PostSlug;
    await page.ApplyRouteAsync("other-agent");
    Assert.Equal(string.Empty, value.Text);
    Assert.Equal((int)AgentConversationFilterKind.Username, kind.SelectedIndex);
    Assert.Null(viewModel.Filter);
  }

  [Fact]
  public async Task DetailHydratesDeepLinkFilterAndDoesNotReloadWhenReturningFromConversation()
  {
    var service = new Service();
    service.ConversationResponses.Enqueue(Task.FromResult(Conversations([Summary("first")], true, "next")));
    service.ConversationResponses.Enqueue(Task.FromResult(Conversations([Summary("older")])));
    var viewModel = new AgentListsViewModel(service);
    var page = DetailPage(viewModel);
    page.SetContext("agent", new AgentConversationFilter(AgentConversationFilterKind.PostSlug, "post-slug"));

    await page.LoadOnFirstAppearanceAsync();
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await page.LoadOnFirstAppearanceAsync();

    Assert.Equal(["first", "older"], viewModel.Rows.Select(row => row.Id));
    Assert.Equal(2, service.ConversationRequests.Count);
    Assert.Equal(new AgentConversationFilter(AgentConversationFilterKind.PostSlug, "post-slug"), service.ConversationRequests[0].Filter);
  }

  [Fact]
  public async Task DirectoryDoesNotReloadWhenReturningFromDetailButExplicitRouteDoes()
  {
    var service = new Service();
    service.AgentResponses.Enqueue(Task.FromResult(Agents([Agent("first")])));
    service.AgentResponses.Enqueue(Task.FromResult(Agents([Agent("refreshed")])));
    service.AgentResponses.Enqueue(Task.FromResult(Agents([Agent("manual-refresh")])));
    var viewModel = new AgentListsViewModel(service);
    var page = ListPage(viewModel);

    await page.LoadOnFirstAppearanceAsync();
    await page.LoadOnFirstAppearanceAsync();
    Assert.Equal(["first"], viewModel.Rows.Select(row => row.Id));

    await page.ApplyRouteAsync();

    Assert.Equal(["refreshed"], viewModel.Rows.Select(row => row.Id));
    await page.LoadOnFirstAppearanceAsync();
    Assert.Equal(["refreshed"], viewModel.Rows.Select(row => row.Id));

    Find<Button>(page, "agent-refresh").SendClicked();
    await WaitUntilAsync(() => viewModel.Rows.Single().Id == "manual-refresh");
  }

  [Fact]
  public async Task DirectoryRetriesItsFirstAppearanceAfterNavigationCancelsTheLoad()
  {
    var service = new Service();
    var pending = new TaskCompletionSource<AgentsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.AgentResponses.Enqueue(pending.Task);
    service.AgentResponses.Enqueue(Task.FromResult(Agents([Agent("retried")])));
    var viewModel = new AgentListsViewModel(service);
    var page = ListPage(viewModel);

    var initial = page.LoadOnFirstAppearanceAsync();
    await WaitUntilAsync(() => viewModel.IsLoading);
    page.CancelPendingLoad();
    await initial;
    await page.LoadOnFirstAppearanceAsync();

    Assert.Equal(["retried"], viewModel.Rows.Select(row => row.Id));
  }

  [Fact]
  public async Task DetailStatusIsHiddenUntilDetailLoadsThenReflectsActiveState()
  {
    var pending = new TaskCompletionSource<AgentDetailResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new Service();
    service.DetailResponses.Enqueue(pending.Task);
    var page = DetailPage(new AgentListsViewModel(service));
    var status = Find<Label>(page, "agent-status");

    Assert.False(status.IsVisible);
    var loading = page.ApplyRouteAsync("agent");
    await WaitUntilAsync(() => !status.IsVisible);
    pending.SetException(new InvalidOperationException("detail unavailable"));
    await loading;
    Assert.False(status.IsVisible);

    service.DetailResponses.Enqueue(Task.FromResult(Detail(active: true, username: null)));
    await page.ApplyRouteAsync("agent");
    Assert.True(status.IsVisible);
    Assert.Equal("Active", status.Text);

    service.DetailResponses.Enqueue(Task.FromResult(Detail(active: false, username: null)));
    await page.ApplyRouteAsync("agent");
    Assert.True(status.IsVisible);
    Assert.Equal("Inactive", status.Text);
  }

  [Fact]
  public async Task DetailLocalizesCreatedAtAndFilterAccessibilitySemantics()
  {
    var createdAt = new DateTimeOffset(2026, 7, 14, 15, 30, 0, TimeSpan.Zero);
    var service = new Service();
    service.DetailResponses.Enqueue(Task.FromResult(new AgentDetailResponse(
        Agent("agent") with { CreatedAt = createdAt }, new PublicUser("user", "account"))));
    using var controller = new UiLocaleController(new LanguageProvider("en"));
    var localization = new UiLocalization(controller);
    var viewModel = new AgentListsViewModel(service, localization, controller);
    var page = DetailPage(viewModel);
    page.SetContext("agent");
    Assert.True(page.MatchesContext("agent"));
    Assert.False(page.MatchesContext("other-agent"));

    await page.ApplyRouteAsync("agent");

    var kind = Find<Picker>(page, "agent-filter-kind");
    var value = Find<Entry>(page, "agent-filter-value");
    Assert.Equal(localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceAgentFilterKind), SemanticProperties.GetDescription(kind));
    Assert.Equal(
        localization.Format(UiMessageKey.NativeSwiftRouteSurfaceAgentFilterValue, ("filter", "Username")),
        SemanticProperties.GetDescription(value));
    Assert.Contains(Labels(page), label => label == localization.FormatDateTime(createdAt, TimeZoneInfo.Local));
    var items = Assert.Single(Descendants<CollectionView>(page));
    var header = Assert.IsType<VerticalStackLayout>(items.Header);
    Assert.Equal("agent-detail-header", header.AutomationId);
    var grid = Assert.Single(Descendants<Grid>(page));
    Assert.Equal(GridLength.Star, grid.RowDefinitions[0].Height);

    kind.SelectedIndex = (int)AgentConversationFilterKind.PostSlug;
    Assert.Equal(
        localization.Format(UiMessageKey.NativeSwiftRouteSurfaceAgentFilterValue, ("filter", "Post slug")),
        SemanticProperties.GetDescription(value));

    controller.ApplySavedLocale("es");
    Assert.Equal("Filtro de conversación", SemanticProperties.GetDescription(kind));
    Assert.Equal("Valor de Slug de publicación", SemanticProperties.GetDescription(value));
    Assert.Contains(Labels(page), label => label == localization.FormatDateTime(createdAt, TimeZoneInfo.Local));
  }

  [Fact]
  public async Task TranscriptRendersLoadingInitialRetryRowsFallbackAndLocalizedTimestamp()
  {
    var service = new Service();
    var pending = new TaskCompletionSource<AgentConversationDetailResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.TranscriptResponses.Enqueue(pending.Task);
    using var controller = new UiLocaleController(new LanguageProvider("es"));
    var localization = new UiLocalization(controller);
    var viewModel = new AgentConversationViewModel(service, localization, controller);
    var page = ConversationPage(viewModel);
    var load = page.ApplyRouteAsync("agent", "conversation");
    await WaitUntilAsync(() => viewModel.IsLoading);
    Assert.True(Find<ActivityIndicator>(page, "agent-conversation-loading").IsVisible);

    pending.SetException(new InvalidOperationException("conversation unavailable"));
    await load;
    Assert.Contains(Labels(page), label => label == localization.Localize(UiMessageKey.NativeDotnetCsharpError));
    service.TranscriptResponses.Enqueue(Task.FromResult(Transcript(
        [Message("later", "assistant", "later", at: 2), Message("earlier", "user", "earlier", at: 1), Message("error", "tool", null, "fallback error", at: 3), Message("empty", "assistant", null, null, at: 4), Message("selected", "user", "selected", at: 5, createdById: "system")], false)));
    Find<Button>(page, "agent-conversation-retry").SendClicked();
    await WaitUntilAsync(() => viewModel.MessageRows.Count == 5);

    var rows = RenderedRows<AgentTranscriptRow>(page, viewModel.MessageRows);
    Assert.Equal(
        localization.FormatDateTime(DateTimeOffset.UnixEpoch, TimeZoneInfo.Local),
        Find<Label>(page, "agent-conversation-created-at").Text);
    Assert.Equal(["earlier", "later", "fallback error", localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceNoResults), "selected"], rows.Select(row => Labels(row).ElementAt(1)));
    Assert.Equal(
        [
          localization.Localize(UiMessageKey.NativeSwiftHouseholdsBookmarksUser),
          localization.Localize(UiMessageKey.NativeSwiftHouseholdsBookmarksUser),
          localization.Localize(UiMessageKey.NativeSwiftHouseholdsBookmarksUser),
          localization.Localize(UiMessageKey.NativeSwiftHouseholdsBookmarksUser),
          localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceAgent),
        ],
        rows.Select(row => Labels(row).First()));
    Assert.Equal(localization.FormatDateTime(DateTimeOffset.UnixEpoch.AddMinutes(1), TimeZoneInfo.Local), Labels(rows[0]).Last());
  }

  [Fact]
  public async Task TranscriptContinuationFailureShowsRetryRetainsRowsAndPrependsOnSuccess()
  {
    var service = new Service();
    service.TranscriptResponses.Enqueue(Task.FromResult(Transcript([Message("current", "assistant", "current", at: 2)], true, "older")));
    var viewModel = new AgentConversationViewModel(service);
    var page = ConversationPage(viewModel);
    await page.ApplyRouteAsync("agent", "conversation");
    service.TranscriptResponses.Enqueue(Task.FromException<AgentConversationDetailResponse>(new InvalidOperationException("older unavailable")));
    Find<Button>(page, "agent-conversation-load-older").SendClicked();
    await WaitUntilAsync(() => viewModel.PaginationErrorMessage is not null);
    Assert.Equal(["current"], viewModel.MessageRows.Select(row => row.Body));
    Assert.Equal("Retry", Find<Button>(page, "agent-conversation-load-older").Text);

    service.TranscriptResponses.Enqueue(Task.FromResult(Transcript([Message("older", "user", "older", at: 1)], false)));
    Find<Button>(page, "agent-conversation-load-older").SendClicked();
    await WaitUntilAsync(() => viewModel.MessageRows.Count == 2);
    Assert.Equal(["older", "current"], viewModel.MessageRows.Select(row => row.Body));
  }

  [Fact]
  public async Task TranscriptRendersLocalizedEmptyState()
  {
    var service = new Service();
    var viewModel = new AgentConversationViewModel(service);
    var page = ConversationPage(viewModel);
    await page.ApplyRouteAsync("agent", "conversation");
    Assert.True(Find<Label>(page, "agent-conversation-empty").IsVisible);
  }

  [Fact]
  public async Task ReplacedSameAgentFilterRequestsCannotCommitOldFirstOrContinuationPages()
  {
    var service = new Service();
    var first = new TaskCompletionSource<AgentConversationsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.ConversationResponses.Enqueue(first.Task);
    var viewModel = new AgentListsViewModel(service);
    var oldLoad = viewModel.LoadConversationsAsync("agent", TestContext.Current.CancellationToken);
    await WaitUntilAsync(() => service.ConversationRequests.Count == 1);
    service.ConversationResponses.Enqueue(Task.FromResult(Conversations([Summary("fresh")])));
    await viewModel.SearchConversationsAsync(
        AgentConversationFilterKind.Username,
        "new",
        TestContext.Current.CancellationToken);
    first.SetResult(Conversations([Summary("stale")]));
    await oldLoad;
    Assert.Equal(["fresh"], viewModel.Rows.Select(row => row.Id));

    service.ConversationResponses.Enqueue(Task.FromResult(Conversations([Summary("current")], true, "older")));
    await viewModel.ClearConversationSearchAsync(TestContext.Current.CancellationToken);
    var continuation = new TaskCompletionSource<AgentConversationsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.ConversationResponses.Enqueue(continuation.Task);
    var oldMore = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await WaitUntilAsync(() => service.ConversationRequests.Count == 4);
    service.ConversationResponses.Enqueue(Task.FromResult(Conversations([Summary("replacement")])));
    await viewModel.SearchConversationsAsync(
        AgentConversationFilterKind.PostSlug,
        "new-post",
        TestContext.Current.CancellationToken);
    continuation.SetResult(Conversations([Summary("stale-page")]));
    await oldMore;
    Assert.Equal(["replacement"], viewModel.Rows.Select(row => row.Id));
  }

  private static AgentListsPage ListPage(AgentListsViewModel viewModel)
  {
    Setup(); return new AgentListsPage(viewModel);
  }

  private static AgentDetailPage DetailPage(AgentListsViewModel viewModel)
  {
    Setup(); return new AgentDetailPage(viewModel);
  }

  private static AgentConversationPage ConversationPage(AgentConversationViewModel viewModel)
  {
    Setup(); return new AgentConversationPage(viewModel);
  }

  private static void Setup()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in UiMessageKey.All) app.Resources[key.Value] = UiLocalization.English.Localize(key);
  }

  private static T Find<T>(Element root, string id) where T : Element =>
      Assert.Single(Descendants<T>(root), item => item.AutomationId == id);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in Children(root))
    {
      if (child is T item) yield return item;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private static IEnumerable<Element> Children(Element root) =>
      ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>()
          .Concat(root is CollectionView { Header: Element header } ? [header] : [])
          .Concat(root is CollectionView { Footer: Element footer } ? [footer] : [])
          .Distinct();

  private static string[] Labels(Element root) => Descendants<Label>(root).Select(label => label.Text ?? string.Empty).ToArray();

  private static View[] RenderedRows<T>(Element page, IReadOnlyList<T> values)
  {
    var list = Assert.Single(Descendants<CollectionView>(page));
    return values.Select(value =>
    {
      var row = Assert.IsAssignableFrom<View>(list.ItemTemplate!.CreateContent());
      row.BindingContext = value;
      return row;
    }).ToArray();
  }

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(10, TestContext.Current.CancellationToken);
    Assert.True(condition());
  }

  private static AgentSummary Agent(string id) => new(id, "system", "helper", null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);
  private static AgentDetailResponse Detail(bool active, string? username) => new(Agent("agent") with { ActivatedAt = active ? DateTimeOffset.UnixEpoch : null }, username is null ? null : new PublicUser("user", username));
  private static AgentsResponse Agents(IReadOnlyList<AgentSummary> rows) => new(rows, new PageInfo(null, false, null), new Dictionary<string, PublicUser>());
  private static AgentConversationsResponse Conversations(IReadOnlyList<AgentConversationSummary> rows, bool hasMore = false, string? cursor = null) => new(rows, new PageInfo(cursor, hasMore, null), new Dictionary<string, PublicUser>());
  private static AgentConversationSummary Summary(string id) => new(id, id, DateTimeOffset.UnixEpoch, "user", DateTimeOffset.UnixEpoch, null, null, null);
  private static AgentConversationDetailResponse Transcript(IReadOnlyList<AgentConversationMessage> rows, bool hasMore = false, string? cursor = null) => new(Conversation(), rows, new PageInfo(cursor, hasMore, null));
  private static AgentConversation Conversation() => new("conversation", "agent", "", DateTimeOffset.UnixEpoch, "u", DateTimeOffset.UnixEpoch, null, null, null, null);
  private static AgentConversationMessage Message(string id, string role, string? body, string? error = null, int at = 0, string createdById = "u") => new(id, "conversation", DateTimeOffset.UnixEpoch.AddMinutes(at), createdById, DateTimeOffset.UnixEpoch, null, null, null, new AgentConversationMessageContent(role, body, error));

  private sealed class Service : IAgentConversationsService
  {
    public Queue<Task<AgentsResponse>> AgentResponses { get; } = [];
    public Queue<Task<AgentDetailResponse>> DetailResponses { get; } = [];
    public Queue<Task<AgentConversationsResponse>> ConversationResponses { get; } = [];
    public Queue<Task<AgentConversationDetailResponse>> TranscriptResponses { get; } = [];
    public List<FetchAgentConversationsRequest> ConversationRequests { get; } = [];
    public Task<AgentsResponse> FetchAgentsAsync(FetchAgentsRequest request, CancellationToken cancellationToken = default) =>
        AgentResponses.TryDequeue(out var response) ? response.WaitAsync(cancellationToken) : Task.FromResult(Agents([]));
    public Task<AgentDetailResponse> FetchAgentAsync(string idOrSlug, CancellationToken cancellationToken = default) =>
        DetailResponses.TryDequeue(out var response) ? response : Task.FromResult(Detail(true, null));
    public Task<AgentConversationsResponse> FetchAgentConversationsAsync(FetchAgentConversationsRequest request, CancellationToken cancellationToken = default)
    {
      ConversationRequests.Add(request);
      return ConversationResponses.TryDequeue(out var response) ? response : Task.FromResult(Conversations([]));
    }
    public Task<AgentConversationDetailResponse> FetchAgentConversationAsync(FetchAgentConversationRequest request, CancellationToken cancellationToken = default) =>
        TranscriptResponses.TryDequeue(out var response) ? response : Task.FromResult(Transcript([]));
  }

  private sealed class LanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
