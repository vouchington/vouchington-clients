using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Engineering;

public sealed partial class AgentConversationViewModel
{
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native load errors become retryable view state.")]
  public async Task LoadAsync(
      string newAgentIdOrSlug,
      string newConversationId,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(newAgentIdOrSlug);
    ArgumentException.ThrowIfNullOrWhiteSpace(newConversationId);
    var generation = Interlocked.Increment(ref contextGeneration);
    Interlocked.Increment(ref pageRequestGeneration);
    agentIdOrSlug = newAgentIdOrSlug;
    conversationId = newConversationId;
    nextCursor = null;
    agentSystemUserId = null;
    Conversation = null;
    Messages = [];
    HasMore = false;
    IsLoadingOlder = false;
    IsLoading = true;
    ErrorMessage = null;
    PaginationErrorMessage = null;
    try
    {
      var agentTask = service.FetchAgentAsync(newAgentIdOrSlug, cancellationToken);
      var conversationTask = service.FetchAgentConversationAsync(
          new FetchAgentConversationRequest(newAgentIdOrSlug, newConversationId),
          cancellationToken);
      await Task.WhenAll(agentTask, conversationTask).ConfigureAwait(true);
      if (!IsCurrentContext(generation, newAgentIdOrSlug, newConversationId)) return;
      agentSystemUserId = (await agentTask.ConfigureAwait(true)).Agent.SystemUserId;
      ApplyPage(await conversationTask.ConfigureAwait(true), replace: true);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception)
    {
      if (IsCurrentContext(generation, newAgentIdOrSlug, newConversationId)) SetLocalizedError(UiText.Localized(UiMessageKey.NativeDotnetCsharpError));
    }
    finally
    {
      if (IsCurrentContext(generation, newAgentIdOrSlug, newConversationId)) IsLoading = false;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Continuation errors preserve rows and become retry state.")]
  public async Task LoadOlderAsync(CancellationToken cancellationToken = default)
  {
    if (!CanLoadOlder || agentIdOrSlug is not { } agent || conversationId is not { } id || nextCursor is not { } after)
    {
      return;
    }

    var generation = Volatile.Read(ref contextGeneration);
    var pageGeneration = Interlocked.Increment(ref pageRequestGeneration);
    IsLoadingOlder = true;
    PaginationErrorMessage = null;
    try
    {
      var response = await service.FetchAgentConversationAsync(
          new FetchAgentConversationRequest(agent, id, after),
          cancellationToken).ConfigureAwait(true);
      if (!IsCurrentPage(generation, pageGeneration, agent, id)) return;
      ApplyPage(response, replace: false);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception)
    {
      if (IsCurrentPage(generation, pageGeneration, agent, id)) SetLocalizedPaginationError(UiText.Localized(UiMessageKey.NativeDotnetCsharpError));
    }
    finally
    {
      if (IsCurrentPage(generation, pageGeneration, agent, id)) IsLoadingOlder = false;
    }
  }

  private void ApplyPage(AgentConversationDetailResponse response, bool replace)
  {
    Conversation = response.Conversation;
    if (replace)
    {
      Messages = DistinctById(response.Results);
    }
    else
    {
      var currentIds = Messages.Select(message => message.Id).ToHashSet(StringComparer.Ordinal);
      var older = DistinctById(response.Results).Where(message => currentIds.Add(message.Id));
      Messages = [.. older, .. Messages];
    }

    nextCursor = response.PageInfo.EndCursor;
    HasMore = response.PageInfo.HasNextPage;
  }

  private bool IsCurrentContext(int generation, string agent, string id) =>
      generation == Volatile.Read(ref contextGeneration) &&
      string.Equals(agentIdOrSlug, agent, StringComparison.Ordinal) &&
      string.Equals(conversationId, id, StringComparison.Ordinal);

  private bool IsCurrentPage(int generation, int pageGeneration, string agent, string id) =>
      IsCurrentContext(generation, agent, id) && pageGeneration == Volatile.Read(ref pageRequestGeneration);

  private static AgentConversationMessage[] DistinctById(IEnumerable<AgentConversationMessage> source) =>
      source.DistinctBy(message => message.Id, StringComparer.Ordinal)
          .OrderBy(message => message.CreatedAt).ThenBy(message => message.Id, StringComparer.Ordinal).ToArray();
}
