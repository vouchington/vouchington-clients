using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.ModerationIntegrity;

public sealed class ReportIntegrityViewModel
    : IntegrityQueueViewModel<ReportIntegrityFlag, ReportIntegrityRow>
{
  private readonly IModerationIntegrityService service;
  private readonly Dictionary<string, int> penaltyCounts = new(StringComparer.Ordinal);

  public ReportIntegrityViewModel(
      IModerationIntegrityService service,
      NavigationViewer viewer,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null,
      IntegrityCapabilities? capabilities = null)
      : base(viewer, localization, localeController, capabilities) =>
      this.service = service ?? throw new ArgumentNullException(nameof(service));

  public bool CanDismiss(string flagId) => CanBeginAction(flagId, Capabilities.CanResolveFlags);
  public bool CanPenalizeReporters(string flagId) =>
      CanBeginAction(flagId, Capabilities.CanApplyPenalties);
  public int? PenalizedUserCount(string flagId) =>
      penaltyCounts.TryGetValue(flagId, out var count) ? count : null;

  public async Task DismissAsync(
      string flagId,
      CancellationToken cancellationToken = default)
  {
    if (!BeginAction(flagId, Capabilities.CanResolveFlags)) return;
    try
    {
      var response = await service.DismissReportFlagAsync(flagId, cancellationToken)
          .ConfigureAwait(true);
      Replace(response.Flag);
      CompleteAction(flagId);
    }
    catch (Exception ex) when (IntegrityMutationFailure.IsAmbiguous(ex))
    {
      await ReconcileAmbiguousAsync(flagId, async token =>
          (await service.FetchReportFlagAsync(flagId, token).ConfigureAwait(true)).Flag)
          .ConfigureAwait(true);
    }
    catch (Exception ex) when (
        IntegrityMutationFailure.IsExpected(ex))
    {
      FailAction(flagId, ex);
    }
  }

  public async Task PenalizeReportersAsync(
      string flagId,
      CancellationToken cancellationToken = default)
  {
    if (!BeginAction(flagId, Capabilities.CanApplyPenalties)) return;
    try
    {
      var response = await service.PenalizeReportersAsync(flagId, cancellationToken)
          .ConfigureAwait(true);
      Replace(response.Flag);
      penaltyCounts[flagId] = response.PenalizedUserCount;
      CompleteAction(flagId);
    }
    catch (Exception ex) when (IntegrityMutationFailure.IsAmbiguous(ex))
    {
      await ReconcileAmbiguousAsync(flagId, async token =>
          (await service.FetchReportFlagAsync(flagId, token).ConfigureAwait(true)).Flag)
          .ConfigureAwait(true);
    }
    catch (Exception ex) when (
        IntegrityMutationFailure.IsExpected(ex))
    {
      FailAction(flagId, ex);
    }
  }

  public async Task ReconcileAsync(string flagId)
  {
    if (!BeginReconciliationAction(flagId)) return;
    try
    {
      var authoritative = await service.FetchReportFlagAsync(flagId, CancellationToken.None)
          .ConfigureAwait(true);
      Replace(authoritative.Flag);
      CompletePenaltyReconciliation(flagId);
    }
    catch (Exception ex) when (IntegrityMutationFailure.IsExpected(ex) ||
        ex is OperationCanceledException)
    {
      RequireReconciliation(flagId);
    }
  }

  protected override string Id(ReportIntegrityFlag flag) => flag.Id;
  protected override ReportIntegrityRow Row(ReportIntegrityFlag flag) =>
      IntegrityFlagPresentation.ReportRow(flag, Localization);
  protected override bool IsResolved(ReportIntegrityFlag flag) =>
      flag.ResolvedAt is not null || flag.Resolution is not null;
  protected override ReportIntegrityFlag ItemFlag(ReportIntegrityRow row) => row.Flag;

  protected override async Task<IntegrityQueuePage<ReportIntegrityFlag>> FetchAsync(
      IntegrityFlagStatus status,
      string? after,
      CancellationToken cancellationToken)
  {
    var response = await service.FetchReportFlagsAsync(
        status, after, cancellationToken: cancellationToken).ConfigureAwait(true);
    return new(response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage);
  }
}
