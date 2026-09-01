using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
namespace Voucha.Client.Core.Communities;

public enum CommunityDetailSurfaceSection
{
  Overview,
  Members,
  Posts,
  News,
  Lists,
  Settings,
  PinnedPosts,
  Applications,
  Invites,
  Modlog,
  Modmail,
  ModerationAnalytics,
  Bans,
  Restrictions,
  ModeratorVacation,
  AiAgents,
  AgentPrompts,
  Moderation,
}
public sealed partial class CommunityDetailViewModel
{
  private IReadOnlyList<CommunitySummaryRow> news = [];
  private IReadOnlyList<CommunitySummaryRow> pinnedPosts = [];
  private IReadOnlyList<CommunitySummaryRow> applications = [];
  private IReadOnlyList<CommunitySummaryRow> invites = [];
  private IReadOnlyList<CommunitySummaryRow> moderation = [];
  private CommunityDetailSurfaceSection selectedSection = CommunityDetailSurfaceSection.Overview;
  private string moderationTransparencyRange = Voucha.Client.Core.Moderation.ModerationTransparencyRange.Default;

  public IReadOnlyList<CommunitySummaryRow> News
  {
    get => news;
    private set => SetProperty(ref news, value);
  }

  public IReadOnlyList<CommunitySummaryRow> PinnedPosts
  {
    get => pinnedPosts;
    private set => SetProperty(ref pinnedPosts, value);
  }

  public IReadOnlyList<CommunitySummaryRow> Applications
  {
    get => applications;
    private set => SetProperty(ref applications, value);
  }

  public IReadOnlyList<CommunitySummaryRow> Invites
  {
    get => invites;
    private set => SetProperty(ref invites, value);
  }

  public IReadOnlyList<CommunitySummaryRow> Moderation
  {
    get => moderation;
    private set => SetProperty(ref moderation, value);
  }

  public CommunityDetailSurfaceSection SelectedSection
  {
    get => selectedSection;
    private set
    {
      if (SetProperty(ref selectedSection, value))
      {
        InvalidateCommunityListRequests();
        InvalidatePendingReportRequests();
      }
    }
  }

  public string ModerationTransparencyRange
  {
    get => moderationTransparencyRange;
    private set => SetProperty(ref moderationTransparencyRange, value);
  }

  public void SetModerationTransparencyRange(string? range) =>
      ModerationTransparencyRange = Voucha.Client.Core.Moderation.ModerationTransparencyRange.ParseOrDefault(range);

  public async Task SelectModerationTransparencyRangeAsync(
      string range,
      CancellationToken cancellationToken = default)
  {
    SetModerationTransparencyRange(range);
    if (SelectedSection == CommunityDetailSurfaceSection.ModerationAnalytics)
    {
      await LoadSelectedSectionAsync(cancellationToken).ConfigureAwait(true);
    }
  }

  public async Task LoadSurfaceAsync(
      string idOrSlug,
      CommunityDetailSurfaceSection section = CommunityDetailSurfaceSection.Overview,
      CancellationToken cancellationToken = default)
  {
    InvalidateCommunityListRequests();
    SelectedSection = section;
    await LoadAsync(idOrSlug, cancellationToken).ConfigureAwait(true);
    await LoadSelectedSectionAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task SelectSectionAsync(
      CommunityDetailSurfaceSection section,
      CancellationToken cancellationToken = default)
  {
    SelectedSection = section;
    await LoadSelectedSectionAsync(cancellationToken).ConfigureAwait(true);
  }

  public async Task LoadSelectedSectionAsync(CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(communityIdOrSlug))
    {
      return;
    }

    try
    {
      if (await TryLoadRouteSpecificModerationSurfaceAsync(cancellationToken).ConfigureAwait(true))
      {
        return;
      }
      if (await TryLoadAdministrativeSurfaceAsync(cancellationToken).ConfigureAwait(true))
      {
        return;
      }

      switch (SelectedSection)
      {
        case CommunityDetailSurfaceSection.News:
          var newsResponse = await service.FetchNewsPageAsync(communityIdOrSlug, null, 25, cancellationToken).ConfigureAwait(true);
          News = newsResponse.Results
              .Select(reference => NewsRow(reference, newsResponse.RssFeedItems, newsResponse.RssFeedItemEmbeds))
              .Where(row => row is not null)
              .Select(row => row!)
              .ToArray();
          communityListEndCursor = newsResponse.PageInfo.EndCursor;
          OnPropertyChanged(nameof(CanLoadMoreCommunityList));
          break;
        case CommunityDetailSurfaceSection.PinnedPosts:
          PinnedPosts = (await service.FetchPinnedPostsAsync(communityIdOrSlug, cancellationToken).ConfigureAwait(true)).PinnedPosts
              .Select(post => Summary(
                  post.PostId,
                  UiText.Verbatim(post.PostId),
                  UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesPinnedPost),
                  UiText.Localized(
                      UiMessageKey.NativeDotnetCsharpCommunitiesOrder,
                      ("value", post.OrderIndex))))
              .ToArray();
          break;
        case CommunityDetailSurfaceSection.Applications:
          await LoadApplicationsAsync(cancellationToken).ConfigureAwait(true);
          Applications = ApplicationRows
              .Select(row => Summary(
                  row.Id,
                  UiText.Verbatim(row.UserId),
                  UiText.Verbatim(row.Status),
                  Verbatim(row.Message)))
              .ToArray();
          break;
        case CommunityDetailSurfaceSection.Invites:
          await LoadInvitesAsync(cancellationToken).ConfigureAwait(true);
          Invites = InviteRows
              .Select(row => Summary(
                  row.Id,
                  row.RecipientText,
                  UiText.Verbatim(row.Status),
                  UiText.Verbatim(row.Code)))
              .ToArray();
          break;
        case CommunityDetailSurfaceSection.Settings:
          await LoadSavedRepliesAsync(cancellationToken).ConfigureAwait(true);
          break;
        default:
          break;
      }
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
    }
    catch (Exception ex) when (HandleLoadException(ex))
    {
    }
  }
}
