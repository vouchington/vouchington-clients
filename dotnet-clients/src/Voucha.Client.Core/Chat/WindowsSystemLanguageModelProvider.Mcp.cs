using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial class WindowsSystemLanguageModelProvider : IMcpLocalChatProvider
{
  public MemberMcpConnectedSnapshot? ConnectedMcpSnapshot() => mcpSession?.ConnectedSnapshot();

  public bool IsCurrent(MemberMcpConnectedSnapshot snapshot) => mcpSession?.IsCurrent(snapshot) == true;

  public bool MatchesAccount(string accountId) => mcpSession?.MatchesAccount(accountId) == true;

  public bool MatchesAccountEpoch(MemberMcpConnectedSnapshot snapshot) =>
      mcpSession?.MatchesAccountEpoch(snapshot) == true;

  public async Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
      string message, IReadOnlyList<LocalLLMResponseInput> history,
      MemberMcpConnectedSnapshot? snapshot, CancellationToken cancellationToken)
  {
    if (!HasTools(snapshot))
      return await GenerateAssistantContentAsync(message, history, cancellationToken).ConfigureAwait(false);
    if (mcpSession?.IsCurrent(snapshot!) != true || !mcpSession.MatchesAccountEpoch(snapshot!))
      throw new OperationCanceledException("MCP account or catalog changed.");
    var readiness = Readiness;
    if (readiness != WindowsSystemLanguageModelReadiness.Ready)
      throw new InvalidOperationException(localization.Localize(StatusKey(readiness)));
    if (runtime is not IWindowsBuiltInToolRuntime builtIn)
      throw new InvalidOperationException("Windows built-in tool runtime is unavailable.");
    var bridge = new MemberMcpToolBridge(snapshot!.Catalog);
    var prompt = WindowsBuiltInPrompt.Compose(history, message, bridge.Tools);
    var turn = await WindowsBuiltInToolLoop.RunAsync(
        prompt, builtIn.ObservePromptAsync, builtIn.GenerateBuiltInAsync,
        async (call, admit, token) =>
        {
          var result = await bridge.ExecuteValidatedResultAsync(
              call.Name, call.ArgumentsJson, mcpSession, snapshot, call.Id, admit, token).ConfigureAwait(false);
          return new WindowsBuiltInToolOutput(result.Envelope, result.FeedPostFact, result.RssItemFact);
        },
        (call, _) =>
        {
          bridge.Validate(call.Name, call.ArgumentsJson);
          return Task.CompletedTask;
        },
        () => mcpSession.IsCurrent(snapshot), null, cancellationToken).ConfigureAwait(false);
    if (turn.Kind == WindowsBuiltInTurnKind.Answer) return AnswerResult(turn);
    return turn.Kind switch
    {
      WindowsBuiltInTurnKind.Retained => throw new WindowsBuiltInResultRetainedException(
          turn.Envelope!, turn.Receipt, turn.RssItemReceipt, turn.CallId),
      WindowsBuiltInTurnKind.Stopped => throw new InvalidOperationException(
          "Windows built-in generation stopped with status " + turn.Status + "."),
      _ => throw new InvalidOperationException("Windows built-in prompt does not fit the model context."),
    };
  }

  private LocalChatGenerationResult AnswerResult(WindowsBuiltInTurn turn)
  {
    if (string.IsNullOrWhiteSpace(turn.Answer))
    {
      if (turn.Envelope is null)
        throw new InvalidOperationException(localization.Localize(
            UiMessageKey.NativeDotnetChatConversationLocalModelEmptyResponse));
      throw new PossibleMcpToolSideEffectException();
    }
    return new(turn.Answer, "windows_foundry", null,
        turn.Receipt is null ? null : [turn.Receipt],
        turn.RssItemReceipt is null ? null : [turn.RssItemReceipt]);
  }

  private static bool HasTools(MemberMcpConnectedSnapshot? snapshot) =>
      snapshot is not null && snapshot.Catalog.ValueKind == JsonValueKind.Object &&
      snapshot.Catalog.TryGetProperty("tools", out var tools) &&
      tools.ValueKind == JsonValueKind.Array && tools.GetArrayLength() > 0;
}
