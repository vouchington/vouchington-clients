using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial class OpenAICompatibleLocalChatProvider
{
  private async Task<LocalChatGenerationResult> GenerateWithMcpAsync(
      string message, IReadOnlyList<LocalLLMResponseInput> history, LocalLLMEndpointProfile profile,
      string? key, MemberMcpConnectedSnapshot snapshot, CancellationToken cancellationToken)
  {
    var session = mcpSession ?? throw new OperationCanceledException("MCP session is unavailable.");
    if (!session.IsCurrent(snapshot)) throw new OperationCanceledException("MCP account or catalog changed.");
    var bridge = new MemberMcpToolBridge(snapshot.Catalog);
    var model = profile.SelectedModel;
    if (bridge.Tools.Count == 0)
    {
      var plain = await responsesClient.GenerateAssistantContentAsync(
          message, history, profile, key, cancellationToken).ConfigureAwait(false);
      if (string.IsNullOrWhiteSpace(plain))
        throw LocalizedException(UiMessageKey.NativeDotnetChatConversationLocalModelEmptyResponse);
      return new(plain, "openai_compatible", model);
    }

    // Facts come only from results the bridge validated for a call this loop admitted, under the
    // snapshot's account and the provider-issued call id; never from model text.
    var completed = new List<(string CallId, MemberMcpValidatedResult Result)>();
    var admitted = 0;
    string? response;
    try
    {
      response = await responsesClient.GenerateAssistantContentWithToolsAsync(
          message, history, profile, key, bridge.Tools, () => session.IsCurrent(snapshot),
          (name, arguments, _) =>
          {
            bridge.Validate(name, arguments);
            return Task.CompletedTask;
          },
          async (name, arguments, callId, token, onAdmitted) =>
          {
            var result = await bridge.ExecuteValidatedResultAsync(name, arguments, session, snapshot, callId,
                () => { admitted++; onAdmitted(); }, token).ConfigureAwait(false);
            completed.Add((callId, result));
            return result.Envelope;
          },
          cancellationToken).ConfigureAwait(false);
    }
    catch (PossibleMcpToolSideEffectException) when (
        completed.Count > 0 && completed.Count == admitted && !cancellationToken.IsCancellationRequested &&
        session.IsCurrent(snapshot) && session.MatchesAccountEpoch(snapshot))
    {
      throw Retained(completed[^1], snapshot);
    }

    if (string.IsNullOrWhiteSpace(response))
    {
      if (completed.Count > 0 && completed.Count == admitted)
        throw Retained(completed[^1], snapshot);
      throw LocalizedException(UiMessageKey.NativeDotnetChatConversationLocalModelEmptyResponse);
    }

    return new(response, "openai_compatible", model,
        NonEmpty(completed.Select(call => call.Result.FeedPostFact)),
        NonEmpty(completed.Select(call => call.Result.RssItemFact)),
        NonEmpty(completed.Select(call => call.Result.ReferralLinkFact)),
        NonEmpty(completed.Select(call => call.Result.Fact)));
  }

  private static T[]? NonEmpty<T>(IEnumerable<T?> facts) where T : class
  {
    var present = facts.OfType<T>().ToArray();
    return present.Length == 0 ? null : present;
  }

  /// <summary>
  /// A completed action must stay visible when model continuation fails. The retained record holds
  /// one call, so a multi-call turn keeps its last completed call.
  /// </summary>
  private static WindowsBuiltInResultRetainedException Retained(
      (string CallId, MemberMcpValidatedResult Result) call, MemberMcpConnectedSnapshot snapshot) =>
      new(call.Result.Envelope, call.Result.FeedPostFact, call.Result.RssItemFact, call.CallId,
          snapshot.AccountId, snapshot.AccountEpoch, call.Result.ReferralLinkFact, call.Result.Fact);
}
