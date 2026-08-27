using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IModerationService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private ModerationRouteContext? context;
  private IReadOnlyList<ModerationRow> items = [];
  private LoadState state = LoadState.Idle;
  private string? errorMessage;

  public ModerationRouteContext? Context
  {
    get => context;
    private set
    {
      if (SetProperty(ref context, value))
      {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HasContext));
        NotifyTransparencyContextChanged();
      }
    }
  }

  public bool HasContext => Context is not null;
  public string Title => localization.Localize(
      Context?.TitleKey ?? UiMessageKey.NativeDotnetModerationModeration);

  public IReadOnlyList<ModerationRow> Items
  {
    get => items;
    private set
    {
      if (SetProperty(ref items, value)) OnPropertyChanged(nameof(HasItems));
    }
  }

  public bool HasItems => Items.Count > 0;

  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value))
      {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(HasError));
      }
    }
  }

  public bool IsLoading => State == LoadState.Loading;
  public bool HasError => State == LoadState.Error;

  public string? ErrorMessage
  {
    get => errorMessage;
    private set => SetProperty(ref errorMessage, value);
  }

  public void SetContext(ModerationRouteContext next)
  {
    ArgumentNullException.ThrowIfNull(next);
    if (Context == next) return;
    Context = next;
    ResetPagination();
  }

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (Context is null) return;
    await LoadModerationPageAsync(replace: true, cancellationToken).ConfigureAwait(true);
  }

  private async Task<ModerationCursorPage> LoadAppealsAsync(string? after, CancellationToken cancellationToken)
  {
    var response = await service.FetchAppealsAsync(Context!.Mine, after, cancellationToken: cancellationToken).ConfigureAwait(true);
    return new(
        response.Appeals.Select(appeal => Row(appeal.Id, UiText.Verbatim(appeal.CaseId ?? appeal.Id), UiText.Verbatim($"{appeal.Status} · {AppealDescription(appeal)}"), "arrow-uturn-left")).ToArray(),
        response.PageInfo);
  }

  private async Task<ModerationCursorPage> LoadDisputesAsync(string? after, CancellationToken cancellationToken)
  {
    var response = await service.FetchDisputesAsync(Context!.Mine, after, cancellationToken: cancellationToken).ConfigureAwait(true);
    return new(
        response.Disputes.Select(dispute => Row(
              dispute.Id,
              UiText.Verbatim(Context!.Mine ? dispute.PostId ?? dispute.Id : dispute.ClaimText ?? dispute.PostId ?? dispute.Id),
              UiText.Verbatim(Context!.Mine
                  ? $"{dispute.Status} · {dispute.Reason ?? localization.Localize(UiMessageKey.NativeDotnetModerationNoReason)}"
                  : $"{dispute.Status} · {dispute.RecommendedAction ?? dispute.Reason ?? localization.Localize(UiMessageKey.NativeDotnetModerationNoRecommendation)}"),
              "scale-balanced"))
          .ToArray(),
        response.PageInfo);
  }

  private async Task<ModerationCursorPage> LoadModlogAsync(string? after, CancellationToken cancellationToken)
  {
    var response = await service.FetchModlogAsync(after, cancellationToken: cancellationToken).ConfigureAwait(true);
    var rows = response.Results
        .Select(reference =>
        {
          if (reference.Id is { } id && response.ModeratorActions.TryGetValue(id, out var action))
          {
            var actorName = action.ActorId is { } actorId && response.Users.TryGetValue(actorId, out var user)
                ? user.Username ?? actorId
                : action.ActorId ?? localization.Localize(UiMessageKey.NativeDotnetModerationSystem);
            var detail = action.Reason ?? action.CommunityId ?? action.PostId ?? action.ReportId ?? action.ReviewDisputeId ?? localization.Localize(UiMessageKey.NativeDotnetModerationGlobal);
            return Row(action.Id, UiText.Verbatim(action.ActionType), UiText.Verbatim($"{actorName} · {detail}"), "clock-arrow-circlepath");
          }

          return Row(
              reference.Id ?? reference.EntityId ?? "",
              UiText.Verbatim(reference.Name ?? reference.Slug ?? reference.EntityId ?? reference.Id ?? localization.Localize(UiMessageKey.NativeDotnetModerationAction)),
              UiText.Verbatim(reference.EntityType ?? localization.Localize(UiMessageKey.NativeDotnetModerationModLog)),
              "clock-arrow-circlepath");
        })
        .ToArray();
    return new(rows, response.PageInfo);
  }

  private async Task<IReadOnlyList<ModerationRow>> LoadAnalyticsAsync(CancellationToken cancellationToken)
  {
    var analytics = await service.FetchAnalyticsAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
    return [
      Row("scope", UiText.Localized(UiMessageKey.NativeDotnetModerationScope), UiText.Verbatim(analytics.Scope.Type)),
      Row("queue", UiText.Localized(UiMessageKey.NativeDotnetModerationQueueVolume), UiText.Localized(UiMessageKey.NativeDotnetResidualPendingTotal, ("pending", analytics.QueueVolume.PendingReports), ("total", analytics.QueueVolume.TotalReports))),
      Row("appeals", UiText.Localized(UiMessageKey.NativeDotnetResidualAppeals), UiText.Localized(UiMessageKey.NativeDotnetResidualClosedCount, ("count", analytics.Appeals.TotalClosed))),
      Row("workload", UiText.Localized(UiMessageKey.NativeDotnetModerationModeratorWorkload), UiText.Localized(UiMessageKey.NativeDotnetResidualModeratorCount, ("count", analytics.ModeratorWorkload.Moderators.Count))),
      Row("automod", UiText.Localized(UiMessageKey.NativeDotnetModerationAutomod), UiText.Localized(UiMessageKey.NativeDotnetResidualActionCount, ("count", analytics.AutomodPerformance.TotalActions))),
      Row("friction", UiText.Localized(UiMessageKey.NativeDotnetModerationNewUserFriction), UiText.Localized(UiMessageKey.NativeDotnetResidualRejectedCount, ("count", analytics.NewUserFriction.RejectedFirstPosts))),
    ];
  }

  private string AppealDescription(ModerationAppeal appeal) =>
      appeal.PostId is not null
          ? localization.Format(UiMessageKey.NativeDotnetResidualPostReference, ("id", appeal.PostId))
          : appeal.CommunityBanId is not null
              ? localization.Format(UiMessageKey.NativeDotnetResidualCommunityBanReference, ("id", appeal.CommunityBanId))
              : appeal.UserWarningId is not null
                  ? localization.Format(UiMessageKey.NativeDotnetResidualWarningReference, ("id", appeal.UserWarningId))
                  : appeal.CommunityId is not null
                      ? localization.Format(UiMessageKey.NativeDotnetResidualCommunityReference, ("id", appeal.CommunityId))
                      : appeal.PostRemovalKind?.ToString() ?? localization.Localize(UiMessageKey.NativeDotnetModerationCase);

  private ModerationRow Row(string id, UiText title, UiText detail, string icon = "shield-alert") =>
      new(id, title, detail, localization, icon);

}
