using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  public string? ActiveBansUserId { get; private set; }
  public CommunityBan? SelectedBanDetail { get; private set; }
  private (string Community, string UserId, long Revision)? pendingLiftAll;

  public async Task LoadActiveBansAsync(string userId, CancellationToken cancellationToken = default)
  {
    InvalidateCommunityListRequests();
    ActiveBansUserId = userId;
    await LoadBanScopeAsync(cancellationToken).ConfigureAwait(true);
  }

  public Task LoadBanHistoryAsync(CancellationToken cancellationToken = default)
  {
    InvalidateCommunityListRequests();
    ActiveBansUserId = null;
    return LoadBanScopeAsync(cancellationToken);
  }

  private async Task LoadBanScopeAsync(CancellationToken cancellationToken)
  {
    pendingLiftAll = null;
    ErrorMessage = null;
    SelectedBanDetail = null;
    OnPropertyChanged(nameof(SelectedBanDetail));
    var community = communityIdOrSlug;
    var userId = ActiveBansUserId;
    var revision = Volatile.Read(ref communityListRevision);
    SetCommunityContinuation(null);
    BanRows = [];
    Moderation = [];
    OnPropertyChanged(nameof(BanRows));
    OnPropertyChanged(nameof(Moderation));
    try
    {
      var response = userId is null
          ? await service.FetchBansPageAsync(community, null, 20, cancellationToken).ConfigureAwait(true)
          : await service.FetchActiveBansAsync(community, userId, null, 20, cancellationToken).ConfigureAwait(true);
      if (community != communityIdOrSlug || userId != ActiveBansUserId || revision != Volatile.Read(ref communityListRevision)) return;
      ApplyBanPage(response, false);
      SetCommunityContinuation(response.PageInfo.EndCursor);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (HttpRequestException)
    {
      if (community == communityIdOrSlug && revision == Volatile.Read(ref communityListRevision))
        ErrorMessage = localization.Resolve(UiText.Localized(UiMessageKey.ModerationCommunityBansDetailLoadFailed));
    }
  }

  private void ApplyBanPage(CommunityBansResponse response, bool append)
  {
    var rows = response.Results.Where(item => item.Id is not null && response.CommunityBans.ContainsKey(item.Id))
        .Select(item => CommunityBanRow.FromBan(response.CommunityBans[item.Id!]));
    BanRows = append ? AppendUnique(BanRows, rows, row => row.Id) : rows.ToArray();
    Moderation = BanRows.Select(BanSummary).ToArray();
    OnPropertyChanged(nameof(BanRows));
    OnPropertyChanged(nameof(Moderation));
  }

  public async Task<bool> LoadBanDetailAsync(string banId, CancellationToken cancellationToken = default)
  {
    if (!BanRows.Any(row => row.Id == banId)) return false;
    var community = communityIdOrSlug;
    var revision = Volatile.Read(ref communityListRevision);
    SelectedBanDetail = null;
    try
    {
      var response = await service.FetchBanDetailAsync(community, banId, cancellationToken).ConfigureAwait(true);
      if (community != communityIdOrSlug || revision != Volatile.Read(ref communityListRevision)) return false;
      SelectedBanDetail = response.CommunityBan;
      OnPropertyChanged(nameof(SelectedBanDetail));
      return true;
    }
    catch (Exception ex) when (ex is HttpRequestException or JsonException)
    {
      ErrorMessage = localization.Resolve(UiText.Localized(UiMessageKey.ModerationCommunityBansDetailLoadFailed));
      return false;
    }
  }

  public void RequestLiftAllBans(string banId)
  {
    pendingLiftAll = BanRows.FirstOrDefault(row => row.Id == banId) is { } row
        ? (communityIdOrSlug, row.UserId, Volatile.Read(ref communityContextRevision)) : null;
  }

  public Task<bool> ConfirmLiftAllBansAsync(bool confirmed, CancellationToken cancellationToken = default)
  {
    var pending = pendingLiftAll;
    pendingLiftAll = null;
    return confirmed && pending is { } target && target.Community == communityIdOrSlug &&
        target.Revision == Volatile.Read(ref communityContextRevision)
        ? LiftBansAsync(() => service.LiftAllBansAsync(target.Community, target.UserId, cancellationToken))
        : Task.FromResult(false);
  }

  public Task<bool> LiftBanAsync(string banId, CancellationToken cancellationToken = default) =>
      BanRows.Any(row => row.Id == banId)
          ? LiftBansAsync(() => service.LiftBanAsync(communityIdOrSlug, banId, cancellationToken))
          : Task.FromResult(false);

  private async Task<bool> LiftBansAsync(Func<Task<CommunityBanLiftResponse>> mutation)
  {
    if (IsLoading) return false;
    InvalidateCommunityListRequests();
    var community = communityIdOrSlug;
    var revision = Volatile.Read(ref communityListRevision);
    BeginMutation();
    try
    {
      var response = await mutation().ConfigureAwait(true);
      if (community != communityIdOrSlug || revision != Volatile.Read(ref communityListRevision)) return false;
      var lifted = response.LiftedBanIds.ToHashSet(StringComparer.Ordinal);
      BanRows = BanRows.Where(row => !lifted.Contains(row.Id)).ToArray();
      Moderation = Moderation.Where(row => !lifted.Contains(row.Id)).ToArray();
      if (SelectedBanDetail is not null && lifted.Contains(SelectedBanDetail.Id)) SelectedBanDetail = null;
      OnPropertyChanged(nameof(SelectedBanDetail));
      OnPropertyChanged(nameof(BanRows));
      OnPropertyChanged(nameof(Moderation));
      CompleteLoad();
      return true;
    }
    catch (VouchaApiException ex)
    {
      ErrorMessage = localization.Resolve(BanLiftError(ex));
      State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
    {
      ErrorMessage = localization.Resolve(UiText.Localized(UiMessageKey.ModerationCommunityBansLiftFailed));
      State = LoadState.Idle;
      return false;
    }
  }

  private static UiText BanLiftError(VouchaApiException exception)
  {
    if (exception.StatusCode == HttpStatusCode.Forbidden && exception.ErrorCode == "COMMUNITY_BAN_LIFT_ALL_FORBIDDEN")
      return UiText.Localized(UiMessageKey.ModerationCommunityBansLiftAllForbidden);
    if (exception.StatusCode == HttpStatusCode.Conflict && exception.ErrorCode == "COMMUNITY_BAN_LIFT_ALL_LIMIT_REACHED")
    {
      try
      {
        using var document = JsonDocument.Parse(exception.ResponseBody ?? "{}");
        if (document.RootElement.TryGetProperty("cap", out var cap) && cap.ValueKind == JsonValueKind.Number && cap.TryGetInt32(out var value) && value > 0)
          return UiText.Localized(UiMessageKey.ModerationCommunityBansLiftAllLimit, ("cap", value));
      }
      catch (JsonException) { }
      return UiText.Localized(UiMessageKey.ModerationCommunityBansLiftAllLimitUnknown);
    }
    return UiText.Localized(UiMessageKey.ModerationCommunityBansLiftFailed);
  }
}
