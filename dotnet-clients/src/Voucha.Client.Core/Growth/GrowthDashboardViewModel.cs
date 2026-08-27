using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Growth;

public sealed class GrowthDashboardViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IGrowthMetricsService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private GrowthMetricsResponse? metrics;
  private LoadState state = LoadState.Idle;
  private GrowthMetricsRange selectedRange = GrowthMetricsRange.ThirtyDays;
  private IReadOnlyList<GrowthMetricRow> rows = [];
  private string? errorMessage;

  public GrowthDashboardViewModel(
      IGrowthMetricsService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public IReadOnlyList<GrowthRangeOption> AvailableRanges =>
  [
    new(T(UiMessageKey.NativeSwiftGrowthDashboardToday), GrowthMetricsRange.Today),
    new(T(UiMessageKey.NativeSwiftGrowthDashboardMessage7d), GrowthMetricsRange.SevenDays),
    new(T(UiMessageKey.NativeSwiftGrowthDashboardMessage30d), GrowthMetricsRange.ThirtyDays),
    new(T(UiMessageKey.NativeSwiftGrowthDashboardMessage90d), GrowthMetricsRange.NinetyDays),
    new(T(UiMessageKey.NativeDotnetGrowthAll), GrowthMetricsRange.All),
  ];

  public GrowthMetricsResponse? Metrics
  {
    get => metrics;
    private set => SetProperty(ref metrics, value);
  }

  public GrowthMetricsRange SelectedRange
  {
    get => selectedRange;
    private set => SetProperty(ref selectedRange, value);
  }

  public IReadOnlyList<GrowthMetricRow> Rows
  {
    get => rows;
    private set => SetProperty(ref rows, value);
  }

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

  public void ApplyInitialRange(GrowthMetricsRange range)
  {
    if (Metrics is not null || string.IsNullOrWhiteSpace(range.Value)) return;
    SelectedRange = range;
  }

  public void ReportUnexpectedError(Exception exception)
  {
    ArgumentNullException.ThrowIfNull(exception);
    ErrorMessage = exception.Message;
    State = LoadState.Error;
  }

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    var expectedRange = SelectedRange;
    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      var fetchedMetrics = await service.FetchGrowthMetricsAsync(expectedRange, cancellationToken).ConfigureAwait(true);
      if (SelectedRange != expectedRange) return;
      Metrics = fetchedMetrics;
      Rows = BuildRows(fetchedMetrics);
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      if (SelectedRange != expectedRange) return;
      State = LoadState.Idle;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (SelectedRange != expectedRange) return;
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
  }

  public async Task SelectRangeAsync(GrowthMetricsRange range, CancellationToken cancellationToken = default)
  {
    if (range == SelectedRange && Metrics is not null) return;
    SelectedRange = range;
    await LoadAsync(cancellationToken).ConfigureAwait(true);
  }

  public void Dispose() => localeSubscription?.Dispose();

  private IReadOnlyList<GrowthMetricRow> BuildRows(GrowthMetricsResponse metrics) =>
  [
    Row(UiMessageKey.NativeDotnetGrowthUsers, UiMessageKey.NativeDotnetGrowthTotalUsers, Count(metrics.UserGrowth.TotalUsers), UiMessageKey.NativeDotnetGrowthRegisteredAccounts),
    Row(UiMessageKey.NativeDotnetGrowthUsers, UiMessageKey.NativeDotnetGrowthNewUsers, Count(metrics.UserGrowth.NewUsers), UiMessageKey.NativeDotnetGrowthSelectedRange),
    Row(UiMessageKey.NativeDotnetGrowthUsers, UiMessageKey.NativeDotnetGrowthDau, Count(metrics.UserGrowth.Dau), UiMessageKey.NativeDotnetGrowthDailyActiveUsers),
    Row(UiMessageKey.NativeDotnetGrowthUsers, UiMessageKey.NativeDotnetGrowthMau, Count(metrics.UserGrowth.Mau), UiMessageKey.NativeDotnetGrowthMonthlyActiveUsers),
    Row(UiMessageKey.NativeDotnetGrowthUsers, UiMessageKey.NativeDotnetGrowthDauMau, Percent(metrics.UserGrowth.DauMauRatio), UiMessageKey.NativeDotnetGrowthStickinessRatio),
    Row(UiMessageKey.NativeDotnetGrowthContent, UiMessageKey.NativeDotnetGrowthTotalPosts, Count(metrics.ContentProduction.TotalPosts), UiMessageKey.NativeDotnetGrowthPublishedPosts),
    Row(UiMessageKey.NativeDotnetGrowthContent, UiMessageKey.NativeDotnetGrowthReviews, Count(metrics.ContentProduction.PostsByType.Review), UiMessageKey.NativeDotnetGrowthReviewPosts),
    Row(UiMessageKey.NativeDotnetGrowthContent, UiMessageKey.NativeDotnetGrowthDataPoints, Count(metrics.ContentProduction.PostsByType.DataPoint), UiMessageKey.NativeDotnetGrowthDataPointPosts),
    Row(UiMessageKey.NativeDotnetGrowthContent, UiMessageKey.NativeDotnetGrowthApprovalRate, Percent(metrics.ContentProduction.ClearanceApprovalRate), UiMessageKey.NativeDotnetGrowthClearanceWorkflow),
    Row(UiMessageKey.NativeDotnetGrowthEngagement, UiMessageKey.NativeDotnetGrowthVotes, Count(metrics.Engagement.VotesCast), UiMessageKey.NativeDotnetGrowthVoteVolume),
    Row(UiMessageKey.NativeDotnetGrowthEngagement, UiMessageKey.NativeDotnetGrowthComments, Count(metrics.Engagement.CommentsCreated), UiMessageKey.NativeDotnetGrowthConversationVolume),
    Row(UiMessageKey.NativeDotnetGrowthEngagement, UiMessageKey.NativeDotnetGrowthFollows, Count(metrics.Engagement.FollowsCreated), UiMessageKey.NativeDotnetGrowthNewFollows),
    Row(UiMessageKey.NativeDotnetGrowthNetwork, UiMessageKey.NativeDotnetGrowthReferralCoefficient, Decimal(metrics.NetworkEffects.ReferralCoefficient), UiMessageKey.NativeDotnetGrowthReferralLoop),
    Row(UiMessageKey.NativeDotnetGrowthNetwork, UiMessageKey.NativeDotnetGrowthTopicCoverage, Percent(metrics.NetworkEffects.TopicCoverageRate), UiMessageKey.NativeDotnetGrowthCoveredTopics),
    Row(UiMessageKey.NativeDotnetGrowthNetwork, UiMessageKey.NativeDotnetGrowthLandingPageVisits, Count(metrics.NetworkEffects.LandingPageVisits), UiMessageKey.NativeDotnetGrowthProfileReach),
    Row(UiMessageKey.NativeDotnetGrowthRevenue, UiMessageKey.NativeDotnetGrowthActiveMemberships, Count(metrics.Revenue.ActiveMemberships), UiMessageKey.NativeDotnetGrowthActivePaidAccounts),
    Row(UiMessageKey.NativeDotnetGrowthRevenue, UiMessageKey.NativeDotnetGrowthMrr, MoneyList(metrics.Revenue.MrrByCurrency), UiMessageKey.NativeDotnetGrowthMonthlyRevenue),
    Row(UiMessageKey.NativeDotnetGrowthRevenue, UiMessageKey.NativeDotnetGrowthChurn, Percent(metrics.Revenue.ChurnRate), UiMessageKey.NativeDotnetGrowthCancellationRate),
    Row(UiMessageKey.NativeDotnetGrowthInfrastructure, UiMessageKey.NativeDotnetGrowthCrawlerSuccess, Percent(metrics.Infrastructure.CrawlerSuccessRate), UiMessageKey.NativeDotnetGrowthFeedCrawlerSuccess),
    Row(UiMessageKey.NativeDotnetGrowthInfrastructure, UiMessageKey.NativeDotnetGrowthQueueThroughput, Count(metrics.Infrastructure.QueueThroughput), UiMessageKey.NativeDotnetGrowthJobsProcessed),
    Row(UiMessageKey.NativeDotnetGrowthInfrastructure, UiMessageKey.NativeDotnetGrowthCacheHitRate, Percent(metrics.Infrastructure.CacheHitRate), UiMessageKey.NativeDotnetGrowthCacheEfficiency),
    Row(UiMessageKey.NativeDotnetGrowthInfrastructure, UiMessageKey.NativeDotnetGrowthAiTokenUsage, Count(metrics.Infrastructure.AiTokenUsage), UiMessageKey.NativeDotnetGrowthModelTokens),
  ];

  private GrowthMetricRow Row(UiMessageKey group, UiMessageKey label, string value, UiMessageKey detail) =>
      new(UiText.Localized(group), UiText.Localized(label), value, UiText.Localized(detail), localization);

  private string T(UiMessageKey key) => localization.Localize(key);
  private string Count(int value) => localization.FormatNumber(value);
  private string Count(long value) => localization.FormatNumber(value);
  private string Count(long? value) => value is null ? T(UiMessageKey.NativeDotnetGrowthUnavailable) : Count(value.Value);
  private string Decimal(double value) => localization.FormatNumber((decimal)value);
  private string Percent(double value) => localization.FormatPercent((decimal)value);
  private string Percent(double? value) => value is null ? T(UiMessageKey.NativeDotnetGrowthUnavailable) : Percent(value.Value);
  private string MoneyList(IEnumerable<ScaledMoneyAggregate> values)
  {
    var formatted = values.Select(value =>
          localization.FormatCurrency(
              value.ScaledAmount,
              value.Currency,
              ScaledMoneyAggregate.RequiredScale))
        .ToArray();
    return formatted.Length == 0 ? localization.FormatNumber(0) : string.Join(", ", formatted);
  }

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(AvailableRanges));
    if (Metrics is not null) Rows = BuildRows(Metrics);
  }
}

public sealed record GrowthRangeOption(string Label, GrowthMetricsRange Range);

public sealed record GrowthMetricRow(
    UiText GroupText,
    UiText LabelText,
    string Value,
    UiText DetailText,
    IUiLocalization Localization)
{
  public string Group => Localization.Resolve(GroupText);
  public string Label => Localization.Resolve(LabelText);
  public string Detail => Localization.Resolve(DetailText);
}
