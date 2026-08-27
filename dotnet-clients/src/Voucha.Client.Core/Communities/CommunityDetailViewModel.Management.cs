using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public IReadOnlyList<CommunityApplicationRow> ApplicationRows { get; private set; } = [];
  public IReadOnlyList<CommunityInviteRow> InviteRows { get; private set; } = [];
  public IReadOnlyList<CommunityBanRow> BanRows { get; private set; } = [];
  public IReadOnlyList<CommunityRestrictionRow> RestrictionRows { get; private set; } = [];
  public IReadOnlyList<CommunityModerationRow> ModerationRows { get; private set; } = [];
  public IReadOnlyList<CommunityAiAgent> AiAgents { get; private set; } = [];
  public IReadOnlyList<CommunityAgentPrompt> AgentPrompts { get; private set; } = [];
  public CommunityMemberVacation? ModeratorVacation { get; private set; }
  public bool SuppressCommunityDigestsWhileOnVacation { get; private set; }

  public async Task LoadApplicationsAsync(CancellationToken cancellationToken = default)
  {
    var response = await service.FetchApplicationsPageAsync(communityIdOrSlug, null, 20, cancellationToken).ConfigureAwait(true);
    ApplicationRows = response.Results
        .Select(reference => reference.Id)
        .Where(id => id is not null && response.CommunityApplications.ContainsKey(id))
        .Select(id => CommunityApplicationRow.FromApplication(response.CommunityApplications[id!]))
        .ToArray();
    OnPropertyChanged(nameof(ApplicationRows));
    SetCommunityContinuation(response.PageInfo.EndCursor);
  }

  public async Task LoadInvitesAsync(CancellationToken cancellationToken = default)
  {
    var response = await service.FetchInvitesPageAsync(communityIdOrSlug, null, 20, cancellationToken).ConfigureAwait(true);
    InviteRows = response.Results
        .Select(reference => reference.Id)
        .Where(id => id is not null && response.CommunityInvites.ContainsKey(id))
        .Select(id => CommunityInviteRow.FromInvite(response.CommunityInvites[id!], localization))
        .ToArray();
    OnPropertyChanged(nameof(InviteRows));
    SetCommunityContinuation(response.PageInfo.EndCursor);
  }

  public async Task LoadBansAsync(CancellationToken cancellationToken = default)
  {
    var response = await service.FetchBansPageAsync(communityIdOrSlug, null, 20, cancellationToken).ConfigureAwait(true);
    BanRows = response.Results
        .Select(reference => reference.Id)
        .Where(id => id is not null && response.CommunityBans.ContainsKey(id))
        .Select(id => CommunityBanRow.FromBan(response.CommunityBans[id!]))
        .ToArray();
    OnPropertyChanged(nameof(BanRows));
    SetCommunityContinuation(response.PageInfo.EndCursor);
  }

  public async Task LoadRestrictionsAsync(CancellationToken cancellationToken = default)
  {
    var response = await service.FetchRestrictionsPageAsync(communityIdOrSlug, null, cancellationToken).ConfigureAwait(true);
    RestrictionRows = response.Results
        .Select(reference => reference.Id)
        .Where(id => id is not null && response.CommunityRestrictions.ContainsKey(id))
        .Select(id => CommunityRestrictionRow.FromRestriction(response.CommunityRestrictions[id!]))
        .ToArray();
    OnPropertyChanged(nameof(RestrictionRows));
    SetCommunityContinuation(response.PageInfo.EndCursor);
  }

  public async Task LoadModerationQueueAsync(CancellationToken cancellationToken = default)
  {
    var response = await service.FetchModerationQueuePageAsync(communityIdOrSlug, null, 20, cancellationToken).ConfigureAwait(true);
    ModerationRows = response.Entries.Select(CommunityModerationRow.FromQueueEntry).ToArray();
    OnPropertyChanged(nameof(ModerationRows));
    SetCommunityContinuation(response.PageInfo.EndCursor);
  }

  public async Task LoadModeratorVacationAsync(CancellationToken cancellationToken = default)
  {
    var response = await service.FetchModeratorVacationAsync(communityIdOrSlug, cancellationToken).ConfigureAwait(true);
    ModeratorVacation = response.Vacation;
    SuppressCommunityDigestsWhileOnVacation = response.SuppressCommunityDigestsWhileOnVacation;
    OnPropertyChanged(nameof(ModeratorVacation));
    OnPropertyChanged(nameof(SuppressCommunityDigestsWhileOnVacation));
  }

  public async Task LoadAiAgentsAsync(CancellationToken cancellationToken = default)
  {
    AiAgents = (await service.FetchAiAgentsAsync(communityIdOrSlug, cancellationToken).ConfigureAwait(true)).CommunityAiAgents;
    OnPropertyChanged(nameof(AiAgents));
  }

  public async Task LoadAgentPromptsAsync(CancellationToken cancellationToken = default)
  {
    AgentPrompts = (await service.FetchAgentPromptsAsync(communityIdOrSlug, cancellationToken).ConfigureAwait(true)).CommunityAgentPrompts;
    OnPropertyChanged(nameof(AgentPrompts));
  }
}
