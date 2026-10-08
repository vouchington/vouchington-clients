using Voucha.Client.Core.Api;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.ModerationIntegrity;

public sealed class VoteIntegrityViewModel
    : IntegrityQueueViewModel<VoteIntegrityFlag, VoteIntegrityRow>
{
  private readonly IModerationIntegrityService service;
  private readonly Dictionary<string, int> penaltyCounts = new(StringComparer.Ordinal);
  private readonly Dictionary<string, HashSet<string>> penaltyBaselineIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> penaltySuppressedIds = new(StringComparer.Ordinal);

  public VoteIntegrityViewModel(
      IModerationIntegrityService service,
      NavigationViewer viewer,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null,
      IntegrityCapabilities? capabilities = null)
      : base(viewer, localization, localeController, capabilities) =>
      this.service = service ?? throw new ArgumentNullException(nameof(service));

  public bool CanResolve(string flagId) => CanBeginAction(flagId, Capabilities.CanResolveFlags);
  public bool CanApplyVoteRingPenalty(string flagId) =>
      CanBeginAction(flagId, Capabilities.CanApplyPenalties) &&
      !penaltyCounts.ContainsKey(flagId) && !penaltySuppressedIds.Contains(flagId);
  public int? PenalizedUserCount(string flagId) =>
      penaltyCounts.TryGetValue(flagId, out var count) ? count : null;

  public async Task ResolveAsync(
      string flagId,
      VoteIntegrityResolution resolution,
      CancellationToken cancellationToken = default)
  {
    if (!BeginAction(flagId, Capabilities.CanResolveFlags)) return;
    try
    {
      var response = await service.ResolveVoteFlagAsync(flagId, resolution, cancellationToken)
          .ConfigureAwait(true);
      Replace(response.Flag);
      CompleteAction(flagId);
    }
    catch (Exception ex) when (IntegrityMutationFailure.IsAmbiguous(ex))
    {
      await ReconcileAmbiguousAsync(flagId, async token =>
          (await service.FetchVoteFlagAsync(flagId, token).ConfigureAwait(true)).Flag)
          .ConfigureAwait(true);
    }
    catch (Exception ex) when (
        IntegrityMutationFailure.IsExpected(ex))
    {
      FailAction(flagId, ex);
    }
  }

  public async Task ApplyVoteRingPenaltyAsync(
      string flagId,
      CancellationToken cancellationToken = default)
  {
    if (!CanApplyVoteRingPenalty(flagId) ||
        !BeginAction(flagId, Capabilities.CanApplyPenalties)) return;
    try
    {
      penaltyBaselineIds[flagId] = await FetchVotePenaltyIdsAsync(flagId, cancellationToken)
          .ConfigureAwait(true);
    }
    catch (Exception ex) when (IntegrityMutationFailure.IsExpected(ex) ||
        ex is InvalidOperationException or OperationCanceledException)
    {
      FailAction(flagId, ex);
      return;
    }
    try
    {
      var response = await service.ApplyVoteRingPenaltyAsync(flagId, cancellationToken)
          .ConfigureAwait(true);
      Replace(response.Flag);
      penaltyCounts[flagId] = response.PenalizedUserCount;
      penaltyBaselineIds.Remove(flagId);
      CompleteAction(flagId);
    }
    catch (Exception ex) when (IntegrityMutationFailure.IsAmbiguous(ex))
    {
      penaltySuppressedIds.Add(flagId);
      RequireReconciliation(flagId);
    }
    catch (Exception ex) when (
        IntegrityMutationFailure.IsExpected(ex))
    {
      penaltyBaselineIds.Remove(flagId);
      FailAction(flagId, ex);
    }
  }

  public async Task ReconcileVotePenaltyAsync(string flagId)
  {
    if (!BeginReconciliationAction(flagId)) return;
    if (!penaltyBaselineIds.TryGetValue(flagId, out var baseline))
    {
      RequireReconciliation(flagId);
      return;
    }
    try
    {
      var authoritative = await FetchVotePenaltyIdsAsync(flagId, CancellationToken.None)
          .ConfigureAwait(true);
      var hasNewPenalty = authoritative.Except(baseline).Any();
      var flag = await service.FetchVoteFlagAsync(flagId, CancellationToken.None)
          .ConfigureAwait(true);
      Replace(flag.Flag);
      if (!hasNewPenalty) penaltySuppressedIds.Remove(flagId);
      penaltyBaselineIds.Remove(flagId);
      CompletePenaltyReconciliation(flagId);
    }
    catch (Exception ex) when (IntegrityMutationFailure.IsExpected(ex) ||
        ex is InvalidOperationException or OperationCanceledException)
    {
      RequireReconciliation(flagId);
    }
  }

  private async Task<HashSet<string>> FetchVotePenaltyIdsAsync(
      string flagId,
      CancellationToken cancellationToken)
  {
    var ids = new HashSet<string>(StringComparer.Ordinal);
    var cursors = new HashSet<string>(StringComparer.Ordinal);
    string? after = null;
    while (true)
    {
      var page = await service.FetchVotePenaltiesAsync(
          IntegrityPenaltyStatus.All, after, sourceFlagId: flagId,
          cancellationToken: cancellationToken).ConfigureAwait(true);
      if (page.FilterScope is not { Source: "flag" } scope ||
          !string.Equals(scope.SourceFlagId, flagId, StringComparison.Ordinal) ||
          page.Results.Any(penalty => !string.Equals(
              penalty.SourceFlagId, flagId, StringComparison.Ordinal)))
        throw new InvalidOperationException("Vote penalty scope was not confirmed by the server.");
      foreach (var penalty in page.Results) ids.Add(penalty.Id);
      if (!page.PageInfo.HasNextPage) return ids;
      var next = page.PageInfo.EndCursor;
      if (string.IsNullOrWhiteSpace(next) || !cursors.Add(next))
        throw new InvalidOperationException("Vote penalty pagination cursor was invalid.");
      after = next;
    }
  }

  protected override string Id(VoteIntegrityFlag flag) => flag.Id;
  protected override VoteIntegrityRow Row(VoteIntegrityFlag flag) =>
      IntegrityFlagPresentation.VoteRow(flag, Localization);
  protected override bool IsResolved(VoteIntegrityFlag flag) =>
      flag.ResolvedAt is not null || flag.Resolution is not null;
  protected override VoteIntegrityFlag ItemFlag(VoteIntegrityRow row) => row.Flag;

  protected override async Task<IntegrityQueuePage<VoteIntegrityFlag>> FetchAsync(
      IntegrityFlagStatus status,
      string? after,
      CancellationToken cancellationToken)
  {
    var response = await service.FetchVoteFlagsAsync(
        status, after, cancellationToken: cancellationToken).ConfigureAwait(true);
    return new(response.Results, response.PageInfo.EndCursor, response.PageInfo.HasNextPage);
  }
}
