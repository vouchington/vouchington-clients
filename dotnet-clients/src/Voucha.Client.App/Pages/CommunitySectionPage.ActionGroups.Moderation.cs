using System.Globalization;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private View BuildAiAgentActions()
  {
    var slug = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesAgentSlug);
    return new HorizontalStackLayout
    {
      Spacing = 8,
      Children =
      {
        slug,
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesEnable).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.EnableAiAgentAsync(slug.Text ?? string.Empty, ct)).ConfigureAwait(true)),
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesDisable).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.DisableAiAgentAsync(slug.Text ?? string.Empty, ct)).ConfigureAwait(true)),
      },
    };
  }

  private View BuildAgentPromptActions()
  {
    var promptId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesPromptId);
    var prompt = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesPrompt);
    var modelName = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesModelName);
    var modelProvider = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesModelProvider);
    return new VerticalStackLayout
    {
      Spacing = 8,
      Children =
      {
        new HorizontalStackLayout { Spacing = 8, Children = { promptId, prompt, modelName, modelProvider } },
        new HorizontalStackLayout
        {
          Spacing = 8,
          Children =
          {
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesCreate).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.CreateAgentPromptAsync(prompt.Text ?? string.Empty, OptionalText(modelName), OptionalText(modelProvider), ct)).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesUpdate).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.UpdateAgentPromptAsync(promptId.Text ?? string.Empty, prompt.Text, ct)).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpDelete).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.DeleteAgentPromptAsync(promptId.Text ?? string.Empty, ct)).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesAllocate).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.AllocateAgentPromptSlotAsync(promptId.Text ?? string.Empty, ct)).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesDeallocate).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.DeallocateAgentPromptSlotAsync(promptId.Text ?? string.Empty, ct)).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesTest).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(async ct => { await viewModel.TestAgentPromptAsync(promptId.Text ?? string.Empty, prompt.Text ?? string.Empty, cancellationToken: ct).ConfigureAwait(true); }).ConfigureAwait(true)),
          },
        },
      },
    };
  }

  private View BuildModerationActions()
  {
    var after = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesAfter);
    var limit = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesLimit, UiText.Verbatim("25"));
    var postId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesPostId);
    var userId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesUserId);
    var reason = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesReason);
    var reportId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesReportId);
    var status = TextField(UiMessageKey.NativeDotnetResidualStatus, UiText.Verbatim("reviewed"));
    return new VerticalStackLayout
    {
      Spacing = 8,
      Children =
      {
        new HorizontalStackLayout { Spacing = 8, Children = { after, limit, ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesRecentActions).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.LoadAutomodRecentActionsAsync(OptionalText(after), int.TryParse(limit.Text, out var parsedLimit) ? parsedLimit : 25, cancellationToken: ct)).ConfigureAwait(true)), ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesModerationResults).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(async ct => { await viewModel.LoadModerationResultsAsync(postId.Text ?? string.Empty, ct).ConfigureAwait(true); }).ConfigureAwait(true)) } },
        new HorizontalStackLayout { Spacing = 8, Children = { postId, userId, reason, reportId, status } },
        new HorizontalStackLayout
        {
          Spacing = 8,
          Children =
          {
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesWarn).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.IssueWarningAsync(userId.Text ?? string.Empty, reason.Text ?? string.Empty, reportId: OptionalText(reportId), cancellationToken: ct)).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesReviewPost).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.ReviewPostAsync(postId.Text ?? string.Empty, status.Text ?? "approved", OptionalText(reason), ct)).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesResolveReport).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.ResolveModerationReportAsync(reportId.Text ?? string.Empty, status.Text ?? "reviewed", ct)).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesConfirmEvasion).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.ConfirmBanEvasionAsync(userId.Text ?? string.Empty, ct)).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesDismissEvasion).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.DismissBanEvasionAsync(userId.Text ?? string.Empty, ct)).ConfigureAwait(true)),
          },
        },
      },
    };
  }

  private View BuildModmailActions()
  {
    var subjectUserId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesSubjectUserId);
    var conversationId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesConversationId);
    var text = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesMessage);
    var assignedModId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesAssignedModeratorId);
    var threadState = LocalizedPicker(
        UiMessageKey.NativeDotnetCsharpCommunitiesNoStateChange,
        UiMessageKey.NativeDotnetCsharpCommunitiesResolve,
        UiMessageKey.NativeDotnetCsharpCommunitiesReopen);
    assignedModId.SetBinding(IsVisibleProperty, nameof(CommunityDetailViewModel.CanModerateCommunity));
    threadState.SetBinding(IsVisibleProperty, nameof(CommunityDetailViewModel.CanModerateCommunity));
    var updateButton = ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesUpdate);
    updateButton.SetBinding(IsVisibleProperty, nameof(CommunityDetailViewModel.CanModerateCommunity));
    updateButton.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.UpdateRoutedModmailThreadAsync(ModmailConversationId(conversationId), OptionalText(assignedModId), SelectedThreadResolvedState(threadState), InitialModmailThreadId, ct)).ConfigureAwait(true);
    return new VerticalStackLayout
    {
      Spacing = 8,
      Children =
      {
        new HorizontalStackLayout { Spacing = 8, Children = { subjectUserId, conversationId, text, assignedModId, threadState } },
        new HorizontalStackLayout
        {
          Spacing = 8,
          Children =
          {
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesOpen).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.OpenModmailAsync(OptionalText(subjectUserId), ct)).ConfigureAwait(true)),
            ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesSend).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.SendRoutedModmailMessageAsync(ModmailConversationId(conversationId), text.Text ?? string.Empty, InitialModmailThreadId, ct)).ConfigureAwait(true)),
            updateButton,
          },
        },
      },
    };
  }

  private static bool? SelectedThreadResolvedState(Picker picker) => picker.SelectedIndex switch
  {
    1 => true,
    2 => false,
    _ => null,
  };

  private string ModmailConversationId(Entry conversationId) =>
      OptionalText(conversationId) ?? InitialModmailThreadId ?? string.Empty;
}
