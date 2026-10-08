using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private bool CanShowActionPanel() =>
      section switch
      {
        CommunityDetailSurfaceSection.Settings => viewModel.CanModerateCommunity,
        CommunityDetailSurfaceSection.Lists or CommunityDetailSurfaceSection.Applications or
            CommunityDetailSurfaceSection.Invites or CommunityDetailSurfaceSection.PinnedPosts or
            CommunityDetailSurfaceSection.Bans or CommunityDetailSurfaceSection.Restrictions or
            CommunityDetailSurfaceSection.ModeratorVacation or CommunityDetailSurfaceSection.AiAgents or
            CommunityDetailSurfaceSection.AgentPrompts or CommunityDetailSurfaceSection.Moderation or
            CommunityDetailSurfaceSection.ModerationAnalytics => viewModel.CanModerateCommunity,
        CommunityDetailSurfaceSection.Members => viewModel.CanManageMembers,
        CommunityDetailSurfaceSection.Modmail => viewModel.CanUseModmail,
        _ => false,
      };

  private View BuildActionPanel() => section switch
  {
    CommunityDetailSurfaceSection.Settings => BuildSettingsActions(),
    CommunityDetailSurfaceSection.Members => BuildMemberActions(),
    CommunityDetailSurfaceSection.Lists => BuildListsActions(),
    CommunityDetailSurfaceSection.Applications => BuildApplicationsActions(),
    CommunityDetailSurfaceSection.Invites => BuildInvitesActions(),
    CommunityDetailSurfaceSection.PinnedPosts => BuildPinnedPostsActions(),
    CommunityDetailSurfaceSection.Bans => BuildBansActions(),
    CommunityDetailSurfaceSection.Restrictions => BuildRestrictionsActions(),
    CommunityDetailSurfaceSection.ModeratorVacation => BuildVacationActions(),
    CommunityDetailSurfaceSection.AiAgents => BuildAiAgentActions(),
    CommunityDetailSurfaceSection.AgentPrompts => BuildAgentPromptActions(),
    CommunityDetailSurfaceSection.Moderation or CommunityDetailSurfaceSection.ModerationAnalytics => BuildModerationActions(),
    CommunityDetailSurfaceSection.Modmail => BuildModmailActions(),
    _ => new Grid(),
  };

  private async Task InvokeAndRenderAsync(Func<CancellationToken, Task> action)
  {
    try
    {
      await action(CancellationToken.None).ConfigureAwait(true);
    }
    catch (Exception ex) when (viewModel.HandleActionException(ex))
    {
    }
    finally
    {
      Render();
    }
  }

  private async Task InvokeAndRenderAsync(Func<CancellationToken, Task<bool>> action)
  {
    try
    {
      await action(CancellationToken.None).ConfigureAwait(true);
    }
    catch (Exception ex) when (viewModel.HandleActionException(ex))
    {
    }
    finally
    {
      Render();
    }
  }

  private static Entry TextField(UiMessageKey placeholder, UiText? initialText = null)
  {
    var entry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, placeholder);
    if (initialText is { } value) entry.Text = UiCopy.Resolve(value);
    return entry;
  }

  private static Button ActionButton(UiMessageKey text) =>
      UiCopy.Bind(new Button(), Button.TextProperty, text);

  private static string? OptionalText(Entry entry) =>
      string.IsNullOrWhiteSpace(entry.Text) ? null : entry.Text;
}
