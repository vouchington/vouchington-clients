using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.ReferralLinks;

public sealed partial class ReferralLinksViewModel
{
  private int BeginLoad()
  {
    var currentRequest = Interlocked.Increment(ref requestId);
    State = LoadState.Loading;
    ErrorMessage = null;
    return currentRequest;
  }

  private void CompleteLoad(int currentRequest, IReadOnlyList<ReferralLinkRow> rows)
  {
    if (!IsCurrentRequest(currentRequest)) return;
    Items = rows;
    State = LoadState.Loaded;
  }

  private void CompleteReferralRows(int currentRequest, IReadOnlyList<ReferralLinkRow> rows)
  {
    if (!IsCurrentRequest(currentRequest)) return;
    LinksEndCursor = null;
    HasMoreLinks = false;
    AnalyticsEndCursor = null;
    HasMoreAnalytics = false;
    CompleteLoad(currentRequest, rows);
  }

  private void CompleteManagedReferralRows(
      int currentRequest,
      IReadOnlyList<ReferralLinkRow> rows,
      PageInfo pageInfo,
      bool append)
  {
    if (!IsCurrentRequest(currentRequest)) return;
    AnalyticsEndCursor = null;
    HasMoreAnalytics = false;
    LinksEndCursor = pageInfo.EndCursor;
    HasMoreLinks = pageInfo.HasNextPage;
    CompleteLoad(currentRequest, GroupManagedRowsByProgram(append ? Items.Concat(rows).ToArray() : rows));
  }

  private void CompleteAnalyticsRows(
      int currentRequest,
      IReadOnlyList<ReferralLinkRow> rows,
      PageInfo pageInfo,
      bool append)
  {
    if (!IsCurrentRequest(currentRequest)) return;
    LinksEndCursor = null;
    HasMoreLinks = false;
    AnalyticsEndCursor = pageInfo.EndCursor;
    HasMoreAnalytics = pageInfo.HasNextPage;
    CompleteLoad(currentRequest, append ? Items.Concat(rows).ToArray() : rows);
  }

  private void CompleteError(int currentRequest, string message)
  {
    if (!IsCurrentRequest(currentRequest)) return;
    LinksEndCursor = null;
    HasMoreLinks = false;
    AnalyticsEndCursor = null;
    HasMoreAnalytics = false;
    Items = [];
    ErrorMessage = message;
    State = LoadState.Error;
  }

  private void CompleteErrorPreservingRows(int currentRequest, string message)
  {
    if (!IsCurrentRequest(currentRequest)) return;
    ErrorMessage = message;
    State = LoadState.Error;
  }

  private void CompleteCanceled(int currentRequest)
  {
    if (!IsCurrentRequest(currentRequest)) return;
    State = LoadState.Idle;
  }

  private bool IsCurrentRequest(int currentRequest) => currentRequest == Volatile.Read(ref requestId);

  private ReferralLinkRow RowFromFeed(ReferralLinkFeedItem item) =>
      new(
          item.Id,
          UiText.Verbatim(item.Label ?? item.ReferralProgramName),
          ParseUri(item.Url),
          UiText.Verbatim(item.UserId),
          Localization: localization);

  private ReferralLinkRow RowFromMine(ReferralLink item) =>
      new(
          item.Id,
          UiText.Verbatim(item.Label ?? item.ReferralProgramName),
          ParseUri(item.Url),
          item.DeactivatedAt is null
              ? UiText.Verbatim(item.ReferralProgramName)
              : UiText.Localized(
                  UiMessageKey.NativeDotnetReferralLinksInactive,
                  ("name", item.ReferralProgramName)),
          item.ReferralProgramId,
          item.ReferralProgramSlug,
          CanManage: true,
          IsActive: item.DeactivatedAt is null,
          ProgramName: item.ReferralProgramName,
          Localization: localization);

  private static ReferralLinkRow[] GroupManagedRowsByProgram(IEnumerable<ReferralLinkRow> rows) =>
      rows
          .OrderBy(row => ManagedRowProgramName(row), StringComparer.CurrentCultureIgnoreCase)
          .ThenBy(row => row.Title, StringComparer.CurrentCultureIgnoreCase)
          .ThenBy(row => row.Id, StringComparer.Ordinal)
          .ToArray();

  private static string ManagedRowProgramName(ReferralLinkRow row) =>
      row.ProgramName ?? row.Detail;

  private ReferralLinkRow RowFromPrioritized(PrioritizedReferralLink item) =>
      new(
          item.Id,
          UiText.Verbatim(item.Label ?? item.Url),
          ParseUri(item.Url),
          UiText.Localized(
              UiMessageKey.NativeDotnetReferralLinksPriorityGroup,
              ("group", item.PriorityGroup)),
          Localization: localization);

  private ReferralLinkRow RowFromTrendingProgram(TrendingReferralProgram item) =>
      new(
          item.Id,
          UiText.Verbatim(item.Id),
          null,
          item.LinkCount is int count
              ? UiText.Localized(
                  UiMessageKey.NativeDotnetReferralLinksActiveLinks,
                  ("count", count))
              : UiText.Localized(UiMessageKey.NativeDotnetReferralLinksTrendingReferralProgram),
          Localization: localization);

  private static Uri? ParseUri(string? value) =>
      value is not null && Uri.TryCreate(value, UriKind.Absolute, out var uri)
          ? uri
          : null;
}
