using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Communities;

internal sealed partial class ScriptedCommunitiesService
{
  public Task ResolveModerationReportAsync(
      string idOrSlug,
      string reportId,
      string status,
      CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("resolve-report", idOrSlug));
    MutationDetails.Add($"{reportId}:{status}");
    return Task.CompletedTask;
  }

  public Task<CommunityModmailThread> OpenModmailAsync(
      string idOrSlug,
      OpenCommunityModmailRequest request,
      CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("modmail-open", idOrSlug));
    MutationDetails.Add(request.SubjectUserId ?? "");
    return Task.FromResult(new CommunityModmailThread(
        "thread-1",
        "modmail",
        "",
        "community-1",
        request.SubjectUserId,
        null,
        null,
        null,
        null,
        request.SubjectUserId,
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
        DateTimeOffset.Parse("2026-07-01T00:00:00Z")));
  }

  public Task<CommunityReportModmailConversation> OpenModmailForReportAsync(
      string idOrSlug,
      string reportId,
      CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("modmail-open-report", idOrSlug));
    MutationDetails.Add(reportId);
    return Task.FromResult(new CommunityReportModmailConversation("conversation-1", "direct_message"));
  }

  public Task<CommunityModmailMessage> SendModmailMessageAsync(
      string idOrSlug,
      string conversationId,
      string text,
      CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("modmail-send", idOrSlug));
    MutationDetails.Add($"{conversationId}:{text}");
    return Task.FromResult(new CommunityModmailMessage(
        "message-1",
        conversationId,
        text,
        "moderator-1",
        "mod",
        DateTimeOffset.Parse("2026-07-01T00:00:00Z")));
  }

  public Task<CommunityModmailThread> UpdateModmailThreadAsync(
      string idOrSlug,
      string conversationId,
      UpdateCommunityModmailThreadRequest request,
      CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("modmail-update", idOrSlug));
    MutationDetails.Add($"{conversationId}:{request.AssignedModId}:{request.Resolved}");
    return Task.FromResult(new CommunityModmailThread(
        conversationId,
        "modmail",
        "",
        "community-1",
        "user-1",
        request.AssignedModId,
        request.AssignedModId is null ? null : DateTimeOffset.Parse("2026-07-02T00:00:00Z"),
        request.Resolved == true ? DateTimeOffset.Parse("2026-07-02T00:00:00Z") : null,
        request.Resolved == true ? "moderator-1" : null,
        "user-1",
        DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
        DateTimeOffset.Parse("2026-07-02T00:00:00Z")));
  }

  public Task EnableAiAgentAsync(string idOrSlug, string agentSlug, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("ai-agent-enable", idOrSlug));
    MutationDetails.Add(agentSlug);
    return Task.CompletedTask;
  }

  public Task DisableAiAgentAsync(string idOrSlug, string agentSlug, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("ai-agent-disable", idOrSlug));
    MutationDetails.Add(agentSlug);
    return Task.CompletedTask;
  }

  public Task<CommunityAgentPrompt> CreateAgentPromptAsync(
      string idOrSlug,
      CommunityAgentPromptUpsertRequest request,
      CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("agent-prompt-create", idOrSlug));
    MutationDetails.Add(request.Prompt);
    return Task.FromResult(NewAgentPrompt("prompt-2", request.Prompt, request.ModelName ?? "gpt-5.4-nano", request.ModelProvider ?? "openai"));
  }

  public Task<CommunityAgentPrompt> UpdateAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CommunityAgentPromptUpdateRequest request,
      CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("agent-prompt-update", idOrSlug));
    MutationDetails.Add($"{promptId}:{request.Prompt}");
    return Task.FromResult(NewAgentPrompt(promptId, request.Prompt ?? "Updated prompt", "gpt-5.4-nano", "openai"));
  }

  public Task DeleteAgentPromptAsync(string idOrSlug, string promptId, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("agent-prompt-delete", idOrSlug));
    MutationDetails.Add(promptId);
    return Task.CompletedTask;
  }

  public Task AllocateAgentPromptSlotAsync(string idOrSlug, string promptId, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("agent-prompt-allocate", idOrSlug));
    MutationDetails.Add(promptId);
    return Task.CompletedTask;
  }

  public Task DeallocateAgentPromptSlotAsync(string idOrSlug, string promptId, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("agent-prompt-deallocate", idOrSlug));
    MutationDetails.Add(promptId);
    return Task.CompletedTask;
  }

  public Task<JsonElement> TestAgentPromptAsync(
      string idOrSlug,
      string promptId,
      CommunityAgentPromptTestRunRequest request,
      CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("agent-prompt-test", idOrSlug));
    MutationDetails.Add($"{promptId}:{request.Text}");
    return Task.FromResult(JsonDocument.Parse("""{"flagged":false}""").RootElement.Clone());
  }

  public Task ConfirmBanEvasionAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("ban-evasion-confirm", idOrSlug));
    MutationDetails.Add(userId);
    return Task.CompletedTask;
  }

  public Task DismissBanEvasionAsync(string idOrSlug, string userId, CancellationToken cancellationToken = default)
  {
    MutationCalls.Add(("ban-evasion-dismiss", idOrSlug));
    MutationDetails.Add(userId);
    return Task.CompletedTask;
  }

  private static CommunityAgentPrompt NewAgentPrompt(string id, string prompt, string modelName, string modelProvider) =>
      new(
          id,
          "community-1",
          "user-1",
          "agent-1",
          prompt,
          modelName,
          modelProvider,
          true,
          ActivatedAt: null,
          DeactivatedAt: null,
          DeletedAt: null,
          DeletedById: null,
          CreatedAt: DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
          UpdatedAt: DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
}
