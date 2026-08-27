using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationViewModel
{
  private IReadOnlyList<ModerationTransparencyBucket> transparencyBuckets = [];
  public bool IsTransparencyContext => Context?.RouteKind == ModerationRouteKind.Transparency;
  public string TransparencyRange => Context?.TransparencyRange ?? ModerationTransparencyRange.Default;

  private void NotifyTransparencyContextChanged()
  {
    OnPropertyChanged(nameof(IsTransparencyContext));
    OnPropertyChanged(nameof(TransparencyRange));
  }

  public async Task SelectTransparencyRangeAsync(string range, CancellationToken cancellationToken = default)
  {
    if (Context?.RouteKind != ModerationRouteKind.Transparency) return;
    var selectedRange = ModerationTransparencyRange.ParseOrDefault(range);
    if (Context.TransparencyRange == selectedRange) return;
    SetContext(Context with { TransparencyRange = selectedRange });
    await LoadAsync(cancellationToken).ConfigureAwait(true);
  }

  private async Task<ModerationCursorPage> LoadTransparencyPageAsync(string? after, CancellationToken cancellationToken)
  {
    if (after is null) transparencyBuckets = [];
    try
    {
      var response = await service.FetchTransparencyAsync(Context!.TransparencyRange, after, cancellationToken).ConfigureAwait(true);
      var responseBuckets = after is null
          ? response.Buckets
          : transparencyBuckets.Concat(response.Buckets).DistinctBy(bucket => $"{bucket.Date}:{bucket.Metric}:{bucket.Category}").ToArray();
      if (response.Buckets.Count == 0)
      {
        if (response.NextCursor is not null)
          return new([], new PageInfo(response.NextCursor, true, null), responseBuckets);
        return new([Row("empty", UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsModerationTransparency), UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyEmpty))], new PageInfo(response.NextCursor, response.NextCursor is not null, null), responseBuckets);
      }
      return new(response.Buckets.Select(bucket => Row(
              $"transparency-{bucket.Date}-{bucket.Metric}-{bucket.Category}",
              TransparencyMetricTitle(bucket.Metric),
              TransparencyBucketDetail(bucket, TransparencyRange),
              "shield"))
          .ToArray(), new PageInfo(response.NextCursor, response.NextCursor is not null, null), responseBuckets);
    }
    catch (VouchaApiException exception) when (exception.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound)
    {
      return new([Row("locked", UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsModerationTransparency), UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsTransparencyLocked), "lock")], new PageInfo(null, false, null), [], true);
    }
  }

  private void RelocalizeTransparencyRows()
  {
    if (!IsTransparencyContext || transparencyBuckets.Count == 0) return;
    Items = transparencyBuckets.Select(bucket => Row(
        $"transparency-{bucket.Date}-{bucket.Metric}-{bucket.Category}",
        TransparencyMetricTitle(bucket.Metric),
        TransparencyBucketDetail(bucket, TransparencyRange),
        "shield")).ToArray();
  }

  private static UiText TransparencyBucketDetail(
      ModerationTransparencyBucket bucket,
      string range) => UiText.Localized(
      UiMessageKey.NativeSwiftCommunityRowsTransparencyBucketDetail,
      ("date", TransparencyReleasedPeriod(bucket.Date, range)),
      ("category", CommunityDetailViewModel.TransparencyCategoryTitle(bucket.Category)),
      ("count", UiText.Number(bucket.Count)));

  internal static UiText TransparencyReleasedPeriod(string date, string range)
  {
    var parsed = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    var value = range == ModerationTransparencyRange.All
        ? new LocalizedMonthYear(parsed)
        : (object)parsed;
    return UiText.Localized(
        UiMessageKey.NativeSwiftCommunityRowsTransparencyReleasedOn,
        ("date", value));
  }

  private static UiText TransparencyMetricTitle(string metric) => metric switch
  {
    "appeals" => UiText.Localized(UiMessageKey.NativeDotnetModerationAppeals),
    "automated_moderation" => UiText.Localized(UiMessageKey.NativeDotnetModerationAutomod),
    "moderation_actions" => UiText.Localized(UiMessageKey.NativeDotnetModerationAction),
    "reports" => UiText.Localized(UiMessageKey.NativeDotnetModerationReports),
    _ => UiText.Localized(UiMessageKey.NativeSwiftCommunityRowsModerationTransparency),
  };
}
