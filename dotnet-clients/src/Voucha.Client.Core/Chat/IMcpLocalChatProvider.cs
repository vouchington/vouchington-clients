using Voucha.Client.Core.Agent;

namespace Voucha.Client.Core.Chat;

/// <summary>One exact MCP catalog snapshot accompanies generation and any persistence retry.</summary>
public interface IMcpLocalChatProvider : ILocalChatProvider
{
  MemberMcpConnectedSnapshot? ConnectedMcpSnapshot();
  bool IsCurrent(MemberMcpConnectedSnapshot snapshot);
  bool MatchesAccount(string accountId);
  bool MatchesAccountEpoch(MemberMcpConnectedSnapshot snapshot);
  Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
      string message, IReadOnlyList<LocalLLMResponseInput> history,
      MemberMcpConnectedSnapshot? snapshot, CancellationToken cancellationToken);
}
