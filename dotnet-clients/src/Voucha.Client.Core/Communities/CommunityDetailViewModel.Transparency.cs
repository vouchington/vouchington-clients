using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityDetailViewModel
{
  private string? transparencyNextCursor;
  private bool isLoadingMoreTransparency;
  private string? transparencyPaginationError;
  private long transparencyRequestRevision;
  public bool CanLoadMoreTransparency => transparencyNextCursor is not null && !isLoadingMoreTransparency && ModerationTransparencyRange == "all";
  public bool IsLoadingMoreTransparency => isLoadingMoreTransparency;
  public string? TransparencyPaginationError => transparencyPaginationError;
  private TransparencyRequest BeginTransparencyRequest()
  {
    var requestRevision = ++transparencyRequestRevision;
    isLoadingMoreTransparency = false;
    transparencyNextCursor = null;
    transparencyPaginationError = null;
    NotifyTransparencyPagination();
    return new(communityIdOrSlug!, ModerationTransparencyRange, SelectedSection, requestRevision);
  }

  private async Task<TransparencyRowsResult> LoadTransparencyRowsAsync(
      TransparencyRequest request,
      CancellationToken cancellationToken)
  {
    try
    {
      var response = await service.FetchModerationTransparencyAsync(request.Community, request.Range, cancellationToken: cancellationToken).ConfigureAwait(true);
      if (!IsCurrentTransparencyContext(request)) return new(null, null);
      transparencyNextCursor = response.NextCursor;
      NotifyTransparencyPagination();
      if (response.Buckets.Count == 0)
      {
        if (response.NextCursor is not null)
        {
          return new([], null);
        }
        return new([Summary("transparency-empty", UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsModerationTransparency), UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyEmpty), UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyEmpty))], null);
      }
      return new(response.Buckets.Select(TransparencyRow).ToArray(), null);
    }
    catch (Exception exception) when (
        exception is VouchaApiException or HttpRequestException or System.Text.Json.JsonException
        && !IsCurrentTransparencyContext(request))
    {
      return new(null, null);
    }
    catch (VouchaApiException exception) when (exception.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound)
    {
      return new(LockedTransparencyRows(), exception.StatusCode);
    }
  }

  public async Task LoadMoreTransparencyAsync(CancellationToken cancellationToken = default)
  {
    if (!CanLoadMoreTransparency || string.IsNullOrWhiteSpace(communityIdOrSlug)) return;
    var requestCommunity = communityIdOrSlug;
    var requestRange = ModerationTransparencyRange;
    var requestSection = SelectedSection;
    var requestCursor = transparencyNextCursor!;
    var requestRevision = transparencyRequestRevision;
    isLoadingMoreTransparency = true;
    transparencyPaginationError = null;
    NotifyTransparencyPagination();
    try
    {
      var response = await service.FetchModerationTransparencyAsync(requestCommunity, requestRange, requestCursor, cancellationToken).ConfigureAwait(true);
      if (!IsCurrentTransparencyContinuation(requestCommunity, requestRange, requestSection, requestCursor, requestRevision)) return;
      var known = Moderation.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
      Moderation = Moderation.Concat(response.Buckets.Select(TransparencyRow).Where(row => known.Add(row.Id))).ToArray();
      transparencyNextCursor = response.NextCursor;
    }
    catch (VouchaApiException exception) when (exception.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound)
    {
      if (IsCurrentTransparencyContinuation(requestCommunity, requestRange, requestSection, requestCursor, requestRevision))
      {
        transparencyNextCursor = null;
        transparencyPaginationError = null;
        Moderation = exception.StatusCode == System.Net.HttpStatusCode.Forbidden && HasDurableSiteModerationRole
            ? [.. Moderation.Where(row => !row.Id.StartsWith("transparency-", StringComparison.Ordinal)), .. LockedTransparencyRows()]
            : LockedTransparencyRows();
      }
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException)
    {
      if (IsCurrentTransparencyContinuation(requestCommunity, requestRange, requestSection, requestCursor, requestRevision))
      {
        transparencyPaginationError = ex.Message;
      }
    }
    finally
    {
      if (IsCurrentTransparencyContext(requestCommunity, requestRange, requestSection, requestRevision))
      {
        isLoadingMoreTransparency = false;
        NotifyTransparencyPagination();
      }
    }
  }

  private bool IsCurrentTransparencyContinuation(string community, string range, CommunityDetailSurfaceSection section, string cursor, long revision) =>
      IsCurrentTransparencyContext(community, range, section, revision) && transparencyNextCursor == cursor;

  private bool IsCurrentTransparencyContext(string community, string range, CommunityDetailSurfaceSection section, long revision) =>
      transparencyRequestRevision == revision && communityIdOrSlug == community && ModerationTransparencyRange == range && SelectedSection == section;

  private bool IsCurrentTransparencyContext(TransparencyRequest request) =>
      IsCurrentTransparencyContext(request.Community, request.Range, request.Section, request.Revision);

  private IReadOnlyList<CommunitySummaryRow> LockedTransparencyRows() =>
      [Summary("transparency-locked", UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsModerationTransparency), UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyLocked), UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyLocked))];

  private sealed record TransparencyRequest(string Community, string Range, CommunityDetailSurfaceSection Section, long Revision);
  private sealed record TransparencyRowsResult(IReadOnlyList<CommunitySummaryRow>? Rows, System.Net.HttpStatusCode? DenialStatus);

  private CommunitySummaryRow TransparencyRow(ModerationTransparencyBucket bucket) => Summary($"transparency-{bucket.Date}-{bucket.Metric}-{bucket.Category}", TransparencyTitle(bucket.Metric), TransparencyCategoryTitle(bucket.Category), UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyBucketDetail, ("date", ModerationViewModel.TransparencyReleasedPeriod(bucket.Date, ModerationTransparencyRange)), ("category", TransparencyCategoryTitle(bucket.Category)), ("count", UiText.Number(bucket.Count))));
  private void NotifyTransparencyPagination() { OnPropertyChanged(nameof(CanLoadMoreTransparency)); OnPropertyChanged(nameof(IsLoadingMoreTransparency)); OnPropertyChanged(nameof(TransparencyPaginationError)); }

  private static UiText TransparencyTitle(string metric) => metric switch
  {
    "appeals" => UiText.Localized(UiMessageKey.NativeDotnetModerationAppeals),
    "automated_moderation" => UiText.Localized(UiMessageKey.NativeDotnetModerationAutomod),
    "moderation_actions" => UiText.Localized(UiMessageKey.NativeDotnetModerationAction),
    "reports" => UiText.Localized(UiMessageKey.NativeDotnetModerationReports),
    _ => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsModerationTransparency),
  };

  internal static UiText TransparencyCategoryTitle(string category) => category switch
  {
    "accept" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesAccept),
    "activate_restriction" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesActivateRestriction),
    "agent_moderation" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesAgentModeration),
    "approve" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesApprove),
    "ban" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesBan),
    "change_role" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesChangeRole),
    "community_ai" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesCommunityAi),
    "deny" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesDeny),
    "dismiss_appeal" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesDismissAppeal),
    "dismiss_report" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesDismissReport),
    "harassment" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesHarassment),
    "illegal_content" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesIllegalContent),
    "lift_ban" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesLiftBan),
    "lift_restriction" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesLiftRestriction),
    "lock" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesLock),
    "misinformation" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesMisinformation),
    "openai_omni" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesOpenAiModeration),
    "other" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesOther),
    "pin" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesPin),
    "post_clearance_reject" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesPostClearanceRejection),
    "reduce" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesReduce),
    "reject" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesReject),
    "remove" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesRemove),
    "remove_member" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesRemoveMember),
    "resolve_appeal" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesResolveAppeal),
    "resolve_report" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesResolveReport),
    "spam" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesSpam),
    "spam_detection" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesSpamDetection),
    "suspend" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesSuspend),
    "unlock" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesUnlock),
    "unpin" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesUnpin),
    "unsuspend" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesUnsuspend),
    "vote_manipulation" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesVoteManipulation),
    "warn" => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesWarn),
    _ => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyCategoriesOther),
  };

}
