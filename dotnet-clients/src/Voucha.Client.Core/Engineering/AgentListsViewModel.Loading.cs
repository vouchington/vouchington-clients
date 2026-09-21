using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Engineering;

public sealed partial class AgentListsViewModel
{
  public Task LoadAgentsAsync(CancellationToken cancellationToken = default)
  {
    Filter = null;
    return LoadAsync(null, cancellationToken);
  }

  public Task LoadConversationsAsync(string agent, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(agent);
    if (agentIdOrSlug is not null && !string.Equals(agentIdOrSlug, agent, StringComparison.Ordinal)) Filter = null;
    return LoadAsync(agent, cancellationToken);
  }

  public Task SearchConversationsAsync(
      AgentConversationFilterKind kind,
      string? value,
      CancellationToken cancellationToken = default) =>
      ReloadConversationsAsync(AgentConversationFilter.Create(kind, value), cancellationToken);

  public Task ClearConversationSearchAsync(CancellationToken cancellationToken = default) =>
      ReloadConversationsAsync(null, cancellationToken);

  public void HydrateConversationFilter(AgentConversationFilter? nextFilter)
  {
    Filter = nextFilter;
    SelectedFilterKindIndex = (int)(nextFilter?.Kind ?? AgentConversationFilterKind.Username);
  }

  private Task ReloadConversationsAsync(AgentConversationFilter? nextFilter, CancellationToken cancellationToken)
  {
    if (agentIdOrSlug is not { } agent) return Task.CompletedTask;
    Filter = nextFilter;
    return LoadAsync(agent, cancellationToken, loadDetail: AgentDetail is null);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native load errors become retryable view state.")]
  private async Task LoadAsync(string? agent, CancellationToken cancellationToken, bool loadDetail = true)
  {
    var generation = Interlocked.Increment(ref contextGeneration);
    Interlocked.Increment(ref pageRequestGeneration);
    agentIdOrSlug = agent;
    var requestFilter = agent is null ? null : Filter;
    OnPropertyChanged(nameof(IsDetail));
    nextCursor = null;
    Rows = [];
    HasMore = false;
    IsLoadingMore = false;
    IsLoading = true;
    ErrorMessage = null;
    PaginationErrorMessage = null;
    if (loadDetail) AgentDetail = null;
    try
    {
      if (agent is not null && loadDetail)
      {
        var detailTask = service.FetchAgentAsync(agent, cancellationToken);
        var conversationsTask = FetchPageAsync(agent, null, requestFilter, cancellationToken);
        await Task.WhenAll(detailTask, conversationsTask).ConfigureAwait(true);
        if (!IsCurrentContext(generation, agent, requestFilter)) return;
        AgentDetail = await detailTask.ConfigureAwait(true);
        var conversationPage = await conversationsTask.ConfigureAwait(true);
        ApplyPage(conversationPage.Rows, conversationPage.PageInfo, conversationPage.Users, replace: true);
        return;
      }
      var page = await FetchPageAsync(agent, null, requestFilter, cancellationToken).ConfigureAwait(true);
      if (!IsCurrentContext(generation, agent, requestFilter)) return;
      ApplyPage(page.Rows, page.PageInfo, page.Users, replace: true);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception) { if (IsCurrentContext(generation, agent, requestFilter)) SetLocalizedError(UiText.Localized(UiMessageKey.NativeDotnetCsharpError)); }
    finally { if (IsCurrentContext(generation, agent, requestFilter)) IsLoading = false; }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Continuation errors preserve rows and become retry state.")]
  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (!CanLoadMore || nextCursor is not { } after) return;
    var agent = agentIdOrSlug;
    var requestFilter = Filter;
    var generation = Volatile.Read(ref contextGeneration);
    var pageGeneration = Interlocked.Increment(ref pageRequestGeneration);
    IsLoadingMore = true;
    PaginationErrorMessage = null;
    try
    {
      var page = await FetchPageAsync(agent, after, requestFilter, cancellationToken).ConfigureAwait(true);
      if (!IsCurrentPage(generation, pageGeneration, agent, requestFilter)) return;
      ApplyPage(page.Rows, page.PageInfo, page.Users, replace: false);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception) { if (IsCurrentPage(generation, pageGeneration, agent, requestFilter)) SetLocalizedPaginationError(UiText.Localized(UiMessageKey.NativeDotnetCsharpError)); }
    finally { if (IsCurrentPage(generation, pageGeneration, agent, requestFilter)) IsLoadingMore = false; }
  }

  private async Task<(IReadOnlyList<AgentListRow> Rows, PageInfo PageInfo, IReadOnlyDictionary<string, PublicUser> Users)> FetchPageAsync(
      string? agent,
      string? after,
      AgentConversationFilter? requestFilter,
      CancellationToken cancellationToken)
  {
    if (agent is null)
    {
      var response = await service.FetchAgentsAsync(new FetchAgentsRequest(after), cancellationToken).ConfigureAwait(true);
      return (response.Results.Select(item => new AgentListRow(
          item.Id, IdPrefix(item.SystemUserId), item.AgentType, AgentNavigationTargets.Detail(item.Slug ?? item.Id),
          UserId: item.SystemUserId,
          CreatedAt: item.CreatedAt,
          IsAgentDirectoryRow: true,
          IsActive: item.ActivatedAt is not null && item.DeactivatedAt is null,
          AgentType: item.AgentType)).ToArray(), response.PageInfo, response.Users);
    }

    var conversations = await service.FetchAgentConversationsAsync(
        new FetchAgentConversationsRequest(agent, after, Filter: requestFilter), cancellationToken).ConfigureAwait(true);
    return (conversations.Results.Select(item =>
    {
      var untitled = string.IsNullOrWhiteSpace(item.Title);
      return new AgentListRow(
          item.Id,
          untitled
              ? localization.Localize(UiMessageKey.NativeSwiftChatConversationTitle)
              : item.Title,
          ConversationCreatorName(item, conversations.Users, localization),
          AgentNavigationTargets.Conversation(agent, item.Id),
          UsesLocalizedUntitledConversationTitle: untitled,
          UserId: item.CreatedById,
          CreatedAt: item.CreatedAt);
    }).ToArray(), conversations.PageInfo, conversations.Users);
  }

  private void ApplyPage(
      IReadOnlyList<AgentListRow> incoming,
      PageInfo pageInfo,
      IReadOnlyDictionary<string, PublicUser> users,
      bool replace)
  {
    var mergedUsers = agentIdOrSlug is null ? agentDirectoryUsers : agentConversationUsers;
    if (replace) mergedUsers.Clear();
    foreach (var (id, user) in users) mergedUsers[id] = user;
    IReadOnlyList<AgentListRow> merged;
    if (replace) merged = incoming.DistinctBy(item => item.Id, StringComparer.Ordinal).ToArray();
    else
    {
      var currentIds = Rows.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
      merged = [.. Rows, .. incoming.DistinctBy(item => item.Id, StringComparer.Ordinal).Where(item => currentIds.Add(item.Id))];
    }
    Rows = PresentRows(merged);
    nextCursor = pageInfo.EndCursor;
    HasMore = pageInfo.HasNextPage;
  }

  private static string ConversationCreatorName(
      AgentConversationSummary conversation,
      IReadOnlyDictionary<string, PublicUser> users,
      IUiLocalization localization) =>
      conversation.CreatedById is null
          ? localization.Localize(UiMessageKey.NativeSwiftPresentationValuesDeleted)
          : users.TryGetValue(conversation.CreatedById, out var creator)
          ? FirstNonBlank(creator.DisplayAccount?.Name, creator.Username, IdPrefix(conversation.CreatedById))
          : IdPrefix(conversation.CreatedById);

  private static string FirstNonBlank(params string?[] values) =>
      values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

  private bool IsCurrentContext(int generation, string? agent, AgentConversationFilter? requestFilter) =>
      generation == Volatile.Read(ref contextGeneration) &&
      string.Equals(agentIdOrSlug, agent, StringComparison.Ordinal) &&
      Equals(Filter, requestFilter);

  private bool IsCurrentPage(int generation, int pageGeneration, string? agent, AgentConversationFilter? requestFilter) =>
      IsCurrentContext(generation, agent, requestFilter) && pageGeneration == Volatile.Read(ref pageRequestGeneration);
}
