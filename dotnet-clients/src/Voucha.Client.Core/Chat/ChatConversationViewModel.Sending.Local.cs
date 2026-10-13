using Voucha.Client.Core.Api;
using Voucha.Client.Core.Agent;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private async Task<bool> SendLocalTurnAsync(
      string trimmed,
      string conversationId,
      string localUserMessageId,
      string localAssistantMessageId,
      IReadOnlyList<LocalLLMResponseInput> history,
      ILocalChatProvider provider,
      ChatProviderStatus selectedProvider,
      PendingLocalTurn? retry,
      int currentRequest,
      CancellationToken cancellationToken)
  {
    ResetStreamingState();
    var streamingTokenSource = await ResetStreamingTokenAsync(cancellationToken).ConfigureAwait(true);
    var mcpProvider = provider as IMcpLocalChatProvider;
    var mcpSnapshot = retry?.McpSnapshot ?? mcpProvider?.ConnectedMcpSnapshot();
    if (retry?.McpSnapshot is { } savedSnapshot &&
        mcpProvider?.MatchesAccount(savedSnapshot.AccountId) != true)
      throw new OperationCanceledException("MCP account is unavailable for persistence retry.");
    using var accountTokenSource = retry is not null || mcpSnapshot is null ? null :
        CancellationTokenSource.CreateLinkedTokenSource(streamingTokenSource.Token,
            mcpSnapshot.AccountCancellation);
    var generationToken = accountTokenSource?.Token ?? streamingTokenSource.Token;

    EnsureCurrentMcpAccount(mcpProvider, mcpSnapshot);

    var generation = retry is null
        ? await GenerateWithSnapshotAsync(provider, mcpProvider, trimmed, history, mcpSnapshot,
            generationToken).ConfigureAwait(true)
        : null;
    if (currentRequest != Volatile.Read(ref requestId)) return false;
    EnsureCurrentMcpAccount(mcpProvider, mcpSnapshot);

    var body = retry?.Body ?? new CreateClientGeneratedChatBody(
        trimmed,
        generation!.AssistantContent,
        generation.ModelProvider,
        localUserMessageId,
        localAssistantMessageId,
        generation.ModelName);
    var pending = retry ?? new PendingLocalTurn(conversationId, selectedProvider, body, mcpSnapshot);
    pendingLocalTurn = pending;

    messages.Add(new ChatMessageRow(
        localAssistantMessageId,
        "assistant",
        body.AssistantContent,
        DateTimeOffset.UtcNow));
    OnPropertyChanged(nameof(Messages));

    try
    {
      EnsureCurrentMcpAccount(mcpProvider, mcpSnapshot);
      await chatService.CreateClientGeneratedChatAsync(
          conversationId,
          body,
          streamingTokenSource.Token).ConfigureAwait(true);
      EnsureCurrentMcpAccount(mcpProvider, mcpSnapshot);
      if (ReferenceEquals(pendingLocalTurn, pending)) pendingLocalTurn = null;
    }
    catch
    {
      RemoveLocalMessage(localAssistantMessageId);
      throw;
    }

    return true;
  }

  private static Task<LocalChatGenerationResult> GenerateWithSnapshotAsync(
      ILocalChatProvider provider, IMcpLocalChatProvider? mcpProvider,
      string message, IReadOnlyList<LocalLLMResponseInput> history,
      MemberMcpConnectedSnapshot? snapshot, CancellationToken token) =>
      mcpProvider is null ? provider.GenerateAssistantContentAsync(message, history, token) :
          mcpProvider.GenerateAssistantContentAsync(message, history, snapshot, token);

  private static void EnsureCurrentMcpAccount(
      IMcpLocalChatProvider? provider, MemberMcpConnectedSnapshot? snapshot)
  {
    if (snapshot is not null && provider?.MatchesAccount(snapshot.AccountId) != true)
      throw new OperationCanceledException("MCP account changed.");
  }

  private void ResetStreamingState() => IsStreaming = true;

  private async Task<CancellationTokenSource> ResetStreamingTokenAsync(CancellationToken cancellationToken)
  {
    var previous = Interlocked.Exchange(ref streamingCts, null);
    if (previous is not null)
    {
      await previous.CancelAsync().ConfigureAwait(true);
      previous.Dispose();
    }

    var next = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var replaced = Interlocked.Exchange(ref streamingCts, next);
    if (replaced is not null)
    {
      await replaced.CancelAsync().ConfigureAwait(true);
      replaced.Dispose();
    }

    return next;
  }
}
