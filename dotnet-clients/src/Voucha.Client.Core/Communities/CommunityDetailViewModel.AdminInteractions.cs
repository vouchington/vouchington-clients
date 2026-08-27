using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public Task<bool> OpenModmailAsync(string? subjectUserId = null, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(
          () => service.OpenModmailAsync(communityIdOrSlug, new OpenCommunityModmailRequest(subjectUserId), cancellationToken),
          cancellationToken);

  public Task<bool> OpenModmailForReportAsync(string reportId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(
          () => service.OpenModmailForReportAsync(communityIdOrSlug, reportId, cancellationToken),
          cancellationToken);

  public Task<bool> SendModmailMessageAsync(
      string conversationId,
      string text,
      CancellationToken cancellationToken = default) =>
      SendModmailMessageCoreAsync(conversationId, text, null, cancellationToken);

  public Task<bool> SendRoutedModmailMessageAsync(
      string conversationId,
      string text,
      string? routedThreadId,
      CancellationToken cancellationToken = default) =>
      SendModmailMessageCoreAsync(conversationId, text, routedThreadId, cancellationToken);

  private Task<bool> SendModmailMessageCoreAsync(
      string conversationId,
      string text,
      string? routedThreadId,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(conversationId))
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpConversationIdRequired);
      State = LoadState.Error;
      return Task.FromResult(false);
    }

    return MutateAndReloadSectionAsync(
          () => service.SendModmailMessageAsync(communityIdOrSlug, conversationId, text, cancellationToken),
          cancellationToken,
          ReloadModmailThreadSurface(routedThreadId));
  }

  public Task<bool> UpdateModmailThreadAsync(
      string conversationId,
      string? assignedModId = null,
      bool? resolved = null,
      CancellationToken cancellationToken = default) =>
      UpdateModmailThreadCoreAsync(conversationId, assignedModId, resolved, null, cancellationToken);

  public Task<bool> UpdateRoutedModmailThreadAsync(
      string conversationId,
      string? assignedModId = null,
      bool? resolved = null,
      string? routedThreadId = null,
      CancellationToken cancellationToken = default) =>
      UpdateModmailThreadCoreAsync(conversationId, assignedModId, resolved, routedThreadId, cancellationToken);

  private Task<bool> UpdateModmailThreadCoreAsync(
      string conversationId,
      string? assignedModId,
      bool? resolved,
      string? routedThreadId,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(conversationId))
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpConversationIdRequired);
      State = LoadState.Error;
      return Task.FromResult(false);
    }

    return MutateAndReloadSectionAsync(
          () => service.UpdateModmailThreadAsync(
              communityIdOrSlug,
              conversationId,
              new UpdateCommunityModmailThreadRequest(assignedModId, resolved),
              cancellationToken),
          cancellationToken,
          ReloadModmailThreadSurface(routedThreadId));
  }

  public Task<bool> EnableAiAgentAsync(string agentSlug, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.EnableAiAgentAsync(communityIdOrSlug, agentSlug, cancellationToken), cancellationToken);

  public Task<bool> DisableAiAgentAsync(string agentSlug, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.DisableAiAgentAsync(communityIdOrSlug, agentSlug, cancellationToken), cancellationToken);

  public Task<bool> CreateAgentPromptAsync(
      string prompt,
      string? modelName = null,
      string? modelProvider = null,
      CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(
          () => service.CreateAgentPromptAsync(
              communityIdOrSlug,
              new CommunityAgentPromptUpsertRequest(prompt, modelName, modelProvider),
              cancellationToken),
          cancellationToken);

  public Task<bool> UpdateAgentPromptAsync(
      string promptId,
      string? prompt = null,
      CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(
          () => service.UpdateAgentPromptAsync(
              communityIdOrSlug,
              promptId,
              new CommunityAgentPromptUpdateRequest(prompt),
              cancellationToken),
          cancellationToken);

  public Task<bool> CreateSavedReplyAsync(
      string title,
      string body,
      CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(
          () => service.CreateSavedReplyAsync(
              communityIdOrSlug,
              new CreateCommunitySavedReplyRequest(title, body),
              cancellationToken),
          cancellationToken);

  public Task<bool> DeleteSavedReplyAsync(string replyId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.DeleteSavedReplyAsync(communityIdOrSlug, replyId, cancellationToken), cancellationToken);

  public Task<bool> DeleteAgentPromptAsync(string promptId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.DeleteAgentPromptAsync(communityIdOrSlug, promptId, cancellationToken), cancellationToken);

  public Task<bool> AllocateAgentPromptSlotAsync(string promptId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.AllocateAgentPromptSlotAsync(communityIdOrSlug, promptId, cancellationToken), cancellationToken);

  public Task<bool> DeallocateAgentPromptSlotAsync(string promptId, CancellationToken cancellationToken = default) =>
      MutateAndReloadSectionAsync(() => service.DeallocateAgentPromptSlotAsync(communityIdOrSlug, promptId, cancellationToken), cancellationToken);

  public Task<JsonElement> TestAgentPromptAsync(
      string promptId,
      string text,
      bool? saveForTraining = null,
      bool? expectedFlagged = null,
      string? expectedReason = null,
      CancellationToken cancellationToken = default) =>
      service.TestAgentPromptAsync(
          communityIdOrSlug,
          promptId,
          new CommunityAgentPromptTestRunRequest(text, saveForTraining, expectedFlagged, expectedReason),
          cancellationToken);

  private Func<CancellationToken, Task>? ReloadModmailThreadSurface(string? routedThreadId) =>
      string.IsNullOrWhiteSpace(routedThreadId)
          ? null
          : ct => LoadModmailThreadSurfaceAsync(communityIdOrSlug, routedThreadId, ct);
}
