using System.Text.Json;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Pagination;
using Voucha.Client.Core.Tests.Moderation;
using Voucha.Client.Core.Tests.ModerationIntegrity;
using Voucha.Client.Core.ModerationIntegrity;
using System.Net;
using Xunit;

namespace Voucha.Client.Core.Tests.Lifecycle;

public sealed class LifecycleScenarioContractTests
{
  [Fact]
  public async Task NativeLifecycleClaimsRunThroughTheirProductionViewModelAdapters()
  {
    var contract = LifecycleScenarioContract.Load();
    var scenarios = contract.ScenariosFor("dotnet");
    Assert.Equal(21, scenarios.Count);

    foreach (var scenario in scenarios)
    {
      var adapter = contract.AdapterFor(scenario, "dotnet");
      JsonElement observation;
      switch (scenario.Family)
      {
        case "moderation-appeal-lifecycle":
          Assert.Equal("dotnet-moderation-appeals-view-model", adapter);
          observation = await RunModerationAppealAsync(scenario.Input, TestContext.Current.CancellationToken);
          break;
        case "forward-pagination" or "forward-pagination-cancellation":
          Assert.Equal("dotnet-cursor-pagination-state", adapter);
          observation = RunPagination(scenario.Input);
          break;
        case "integrity-ambiguous-reconciliation":
          Assert.Equal("dotnet-integrity-reconciliation", adapter);
          observation = await RunIntegrityAmbiguousAsync(scenario.Input, TestContext.Current.CancellationToken);
          break;
        default:
          throw new InvalidOperationException($"Unhandled lifecycle family {scenario.Family}.");
      }
      Assert.True(
          JsonElement.DeepEquals(scenario.Expected, observation),
          $"Normalized observation did not match {scenario.Id}.");
    }
  }

  [Fact]
  public async Task ModerationAdapterRejectsMissingQueueRowAfterApproval()
  {
    using var document = JsonDocument.Parse("""
        {
          "preconditions": {
            "status": "pending",
            "approvedAt": null,
            "sentAt": null,
            "viewerRole": "administrator"
          },
          "action": { "type": "approve" },
          "serverOutcome": {
            "status": "dismissed",
            "approvedAt": null,
            "sentAt": null,
            "latestLifecycleChangeChanged": true
          }
        }
        """);

    var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        RunModerationAppealAsync(document.RootElement.Clone(), TestContext.Current.CancellationToken));

    Assert.Equal("Production moderation view model removed appeal appeal-1 after approve.", error.Message);
  }

  private static async Task<JsonElement> RunIntegrityAmbiguousAsync(
      JsonElement input, CancellationToken cancellationToken)
  {
    var action = input.GetProperty("action").GetProperty("type").GetString();
    const string flagId = "flag-contract";
    if (action is "resolve-report" or "apply-report-penalty")
    {
      var service = new ModerationIntegrityTestService();
      var penaltyCount = input.GetProperty("serverOutcome").TryGetProperty(
          "exactReadPenaltyCount", out var count) ? count.GetInt32() : 1;
      var resolved = ModerationIntegrityTestService.ReportFlag(
          flagId, action == "resolve-report" ? "dismissed" : penaltyCount > 0 ? "penalized" : null,
          resolvedAt: action == "apply-report-penalty" && penaltyCount == 0 ? null : DateTimeOffset.UtcNow);
      service.FetchReports = (_, _, _) => Task.FromResult(
          ModerationIntegrityTestService.ReportPage([ModerationIntegrityTestService.ReportFlag(flagId)]));
      service.FetchReport = (_, _) =>
      {
        if (input.GetProperty("serverOutcome").TryGetProperty("exactRead", out _))
          return Task.FromException<ReportIntegrityFlagResponse>(new HttpRequestException("exact read failed"));
        return Task.FromResult(new ReportIntegrityFlagResponse(resolved));
      };
      service.DismissReport = (_, _) => Task.FromException<ReportIntegrityFlagResponse>(
          new HttpRequestException("ambiguous", null, HttpStatusCode.InternalServerError));
      service.PenalizeReporters = (_, _) => Task.FromException<ReportIntegrityPenaltyResponse>(
          new HttpRequestException("ambiguous", null, HttpStatusCode.InternalServerError));
      var model = new ReportIntegrityViewModel(service, new NavigationViewer(true, ["administrator"]));
      await model.LoadAsync(cancellationToken);
      if (action == "resolve-report") await model.DismissAsync(flagId, cancellationToken);
      else await model.PenalizeReportersAsync(flagId, cancellationToken);
      var displayed = model.Items.SingleOrDefault(item => item.Flag.Id == flagId)?.Flag;
      var reconciliationRequired = model.NeedsReconciliation(flagId);
      var hasReconciliationError = model.ActionError(flagId) is not null;
      var committed = displayed?.ResolvedAt is not null || displayed?.Resolution is not null ||
          (displayed is null && !reconciliationRequired);
      var pending = displayed?.ResolvedAt is null && displayed?.Resolution is null;
      var canRetry = reconciliationRequired && hasReconciliationError;
      var canApplyPenalty = displayed is not null && model.CanPenalizeReporters(flagId);
      var unknown = canRetry || (pending && canApplyPenalty);
      return action == "resolve-report"
          ? IntegrityObservation("reportStatus", committed ? "resolved" : "pending", !unknown,
              canRetry ? ["retry"] : [], canRetry ? new { strategy = "fail-closed" } : new { strategy = "exact-read", committed })
          : IntegrityObservation("penaltyApplied", committed, !unknown,
              canRetry ? ["retry"] : committed ? ["revoke"] : ["apply-penalty"],
              canRetry ? new { strategy = "fail-closed" } : new { strategy = "exact-read", committed });
    }
    if (action == "apply-vote-penalty")
    {
      var service = new ModerationIntegrityTestService();
      var baseline = input.GetProperty("preconditions").GetProperty("baselinePenaltyIds")
          .EnumerateArray().Select(item => item.GetString()!).ToHashSet();
      var exact = input.GetProperty("serverOutcome").GetProperty("exactReadPenaltyIds")
          .EnumerateArray().Select(item => item.GetString()!).ToHashSet();
      var reads = 0;
      service.FetchVotes = (_, _, _) => Task.FromResult(ModerationIntegrityTestService.VotePage([
          ModerationIntegrityTestService.VoteFlag(flagId)]));
      service.FetchVotePenalties = (_, _, _, _) => Task.FromResult(VotePenaltyPage(
          ++reads == 1 ? baseline : exact, flagId));
      service.PenalizeVotes = (_, _) => Task.FromException<VoteIntegrityPenaltyApplicationResponse>(
          new HttpRequestException("ambiguous", null, HttpStatusCode.InternalServerError));
      var model = new VoteIntegrityViewModel(service, new NavigationViewer(true, ["administrator"]));
      await model.LoadAsync(cancellationToken);
      await model.ApplyVoteRingPenaltyAsync(flagId, cancellationToken);
      await model.ReconcileVotePenaltyAsync(flagId);
      var committed = !model.CanApplyVoteRingPenalty(flagId) && !model.NeedsReconciliation(flagId);
      return IntegrityObservation("penaltyApplied", committed, committed,
          committed ? ["revoke"] : ["apply-penalty"], new { strategy = "exact-read", comparison = committed ? "new-row" : "unchanged-baseline" });
    }
    // Revocation uses the dedicated production ledger. Its exact GET is the authority after an
    // ambiguous DELETE, for both report and vote penalties.
    var report = action == "revoke-report-penalty";
    return report
        ? await RunReportRevocationAsync(input, cancellationToken)
        : action == "revoke-vote-penalty"
          ? await RunVoteRevocationAsync(input, cancellationToken)
          : throw new InvalidOperationException($"Unhandled integrity action {action}.");
  }

  private static JsonElement IntegrityObservation(
      string stateName, object value, bool committed, string[] actions, object reconciliation)
  {
    var visible = new Dictionary<string, object?> { [stateName] = value, ["error"] = committed ? null : "mutation-outcome-unknown" };
    return JsonSerializer.SerializeToElement(new
    {
      visibleState = visible,
      availableActions = actions,
      reconciliation,
      cancellation = new { behavior = "not-applicable" },
    });
  }

  private static async Task<JsonElement> RunModerationAppealAsync(
      JsonElement input, CancellationToken cancellationToken)
  {
    var service = new ModerationAppealsTestService
    {
      Current = AppealFrom(input.GetProperty("preconditions")),
    };
    var viewer = input.GetProperty("preconditions").GetProperty("viewerRole").GetString();
    var viewModel = new ModerationAppealsViewModel(
        service,
        new NavigationViewer(true, [viewer ?? "administrator"]));
    await viewModel.LoadAsync(cancellationToken);
    var before = Assert.Single(viewModel.Appeals);
    var beforeChangeId = before.LatestLifecycleChangeId;
    service.Current = AppealFrom(input.GetProperty("serverOutcome"), before);
    var action = input.GetProperty("action").GetProperty("type").GetString();
    switch (action)
    {
      case "rerun":
        service.Details.Enqueue(service.Current with
        {
          AiDraftedAt = before.AiDraftedAt!.Value.AddMinutes(1),
        });
        await viewModel.RerunAsync(before, cancellationToken);
        break;
      case "approve":
        await viewModel.ApproveAsync(before, cancellationToken);
        break;
      case "send":
        await viewModel.DeliverAsync(before, cancellationToken);
        break;
      case "deny":
        await viewModel.ResolveAsync(before, ModerationAppealAction.Deny, cancellationToken);
        break;
      case "inspect-actions":
        break;
      default:
        throw new InvalidOperationException($"Unhandled moderation action {action}.");
    }

    ModerationAppeal current;
    if (action == "deny")
    {
      Assert.Empty(viewModel.Appeals);
      current = service.Current;
    }
    else
    {
      current = viewModel.Appeals.SingleOrDefault(appeal => appeal.Id == before.Id)
          ?? throw new InvalidOperationException(
              $"Production moderation view model removed appeal {before.Id} after {action}.");
    }
    var visibleState = new Dictionary<string, object?> { ["status"] = current.Status.ToString().ToLowerInvariant() };
    if (action is "approve" or "send") visibleState["approved"] = current.ApprovedAt is not null;
    if (action is "approve" or "send") visibleState["sent"] = current.SentAt is not null;
    if (action == "deny") visibleState["resolutionAction"] = current.ResolutionAction?.ToString().ToLowerInvariant();
    if (action == "inspect-actions") visibleState["viewerRole"] = viewer;
    if (action is not "inspect-actions") visibleState["latestLifecycleChangeChanged"] = current.LatestLifecycleChangeId != beforeChangeId;
    return JsonSerializer.SerializeToElement(new
    {
      visibleState,
      availableActions = AvailableActions(viewModel, current),
      reconciliation = new { strategy = action == "inspect-actions" ? "not-applicable" : "server-response" },
      cancellation = new { behavior = "not-applicable" },
    });
  }

  private static ModerationAppeal AppealFrom(JsonElement source, ModerationAppeal? baseline = null)
  {
    var service = new ModerationAppealsTestService();
    var status = source.TryGetProperty("status", out var statusElement)
      ? Enum.Parse<ModerationAppealStatus>(statusElement.GetString()!, ignoreCase: true)
      : ModerationAppealStatus.Pending;
    var approved = source.TryGetProperty("approvedAt", out var approvedElement)
        && approvedElement.ValueKind != JsonValueKind.Null;
    var appeal = service.Appeal(
        status: status,
        response: "Ready for delivery",
        approved: approved,
        suspension: source.TryGetProperty("appealType", out var type)
          && type.GetString() == "suspension");
    if (source.TryGetProperty("sentAt", out var sentElement) && sentElement.ValueKind != JsonValueKind.Null)
      appeal = appeal with { SentAt = DateTimeOffset.Parse(sentElement.GetString()!) };
    if (source.TryGetProperty("latestLifecycleChangeChanged", out var changed) && changed.GetBoolean())
      appeal = appeal with { LatestLifecycleChangeId = "change-2" };
    if (source.TryGetProperty("resolutionAction", out var resolution))
      appeal = appeal with { ResolutionAction = Enum.Parse<ModerationAppealAction>(resolution.GetString()!, true) };
    return baseline is null ? appeal : appeal with
    {
      Id = baseline.Id,
      AiDraftedAt = baseline.AiDraftedAt,
      PublicResponse = baseline.PublicResponse,
    };
  }

  private static string[] AvailableActions(
      ModerationAppealsViewModel viewModel, ModerationAppeal appeal)
  {
    return new[] {
      ("rerun", viewModel.CanRerun(appeal)), ("send", viewModel.CanDeliver(appeal)),
      ("accept", viewModel.CanResolve(appeal, ModerationAppealAction.Accept)),
      ("reduce", viewModel.CanResolve(appeal, ModerationAppealAction.Reduce)),
      ("deny", viewModel.CanResolve(appeal, ModerationAppealAction.Deny)),
      ("approve", viewModel.CanApprove(appeal)),
    }.Where(entry => entry.Item2).Select(entry => entry.Item1).ToArray();
  }

  private static JsonElement RunPagination(JsonElement input)
  {
    var preconditions = input.GetProperty("preconditions");
    var items = preconditions.GetProperty("itemIds").EnumerateArray()
        .Select(item => new PageItem(item.GetString()!)).ToArray();
    var state = new CursorPaginationState<PageItem, string>(item => item.Id, items);
    var cursor = preconditions.GetProperty("nextCursor").GetString();
    state.RestoreContinuation(cursor, cursor is not null);
    var action = input.GetProperty("action").GetProperty("type").GetString();
    string strategy;
    string[] actions;
    string cancellation = "not-applicable";
    Dictionary<string, object?> visible;
    if (action == "load-more")
    {
      var request = Assert.IsType<CursorPageRequest>(state.BeginNextPage());
      var outcome = input.GetProperty("serverOutcome");
      if (outcome.TryGetProperty("error", out _)) { state.Fail(request, "network"); strategy = "preserve-and-retry"; actions = ["retry"]; }
      else
      {
        var next = outcome.GetProperty("nextCursor").GetString();
        state.Complete(request, outcome.GetProperty("itemIds").EnumerateArray()
            .Select(item => new PageItem(item.GetString()!)), next, next is not null);
        strategy = "deduplicate-by-id"; actions = state.CanAutomaticallyLoad ? ["load-more"] : [];
      }
    }
    else if (action == "load-more-twice")
    {
      var started = new[] { state.BeginNextPage(), state.BeginNextPage() }.Count(request => request is not null);
      visible = new() { ["requestsStarted"] = started, ["loading"] = state.IsLoading };
      return PaginationObservation(visible, [], "single-flight", cancellation);
    }
    else if (action == "reset-during-load")
    {
      var stale = Assert.IsType<CursorPageRequest>(state.BeginNextPage());
      var outcome = input.GetProperty("serverOutcome");
      var next = outcome.GetProperty("nextCursor").GetString();
      state.Reset(outcome.GetProperty("resetItemIds").EnumerateArray().Select(item => new PageItem(item.GetString()!)));
      state.RestoreContinuation(next, next is not null);
      if (outcome.TryGetProperty("staleError", out _)) state.Fail(stale, "network");
      else state.Complete(stale, outcome.GetProperty("staleItemIds").EnumerateArray()
          .Select(item => new PageItem(item.GetString()!)), null, false);
      strategy = "discard-stale-generation"; actions = state.CanAutomaticallyLoad ? ["load-more"] : [];
    }
    else if (action == "remove")
    {
      state.Remove(item => item.Id == input.GetProperty("action").GetProperty("itemId").GetString());
      strategy = "preserve-continuation"; actions = state.CanAutomaticallyLoad ? ["load-more"] : [];
    }
    else if (action == "cancel-load-more")
    {
      state.Cancel(Assert.IsType<CursorPageRequest>(state.BeginNextPage()));
      strategy = "preserve-state"; actions = state.CanAutomaticallyLoad ? ["load-more"] : []; cancellation = "preserve-items-and-cursor";
    }
    else throw new InvalidOperationException($"Unhandled pagination action {action}.");
    visible = new() { ["itemIds"] = state.Items.Select(item => item.Id).ToArray(), ["nextCursor"] = state.EndCursor };
    if (action is "load-more" && state.LastError is not null || action is "reset-during-load" || action == "cancel-load-more")
      visible["error"] = state.LastError;
    return PaginationObservation(visible, actions, strategy, cancellation);
  }

  private static JsonElement PaginationObservation(Dictionary<string, object?> visibleState, string[] actions, string strategy, string cancellation) =>
      JsonSerializer.SerializeToElement(new { visibleState, availableActions = actions, reconciliation = new { strategy }, cancellation = new { behavior = cancellation } });

  private static async Task<JsonElement> RunReportRevocationAsync(
      JsonElement input, CancellationToken token)
  {
    var service = new ModerationIntegrityTestService();
    var id = input.GetProperty("preconditions").GetProperty("penaltyId").GetString()!;
    var active = new ReportAbusePenalty(id, "user", "reason", null, "admin", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    var revoked = active with { RevokedAt = input.GetProperty("serverOutcome").GetProperty("exactReadRevoked").GetBoolean() ? DateTimeOffset.UtcNow : null };
    service.FetchReportPenalties = (_, _, _) => Task.FromResult(new ReportIntegrityPenaltiesResponse([active], new PageInfo(null, false, id)));
    service.RevokeReportPenalty = (_, _) => Task.FromException<ReportAbusePenaltyResponse>(new HttpRequestException("ambiguous", null, HttpStatusCode.InternalServerError));
    service.FetchReportPenalty = (_, _) => Task.FromResult(new ReportAbusePenaltyResponse(revoked));
    var model = new ReportIntegrityPenaltyViewModel(service, new NavigationViewer(true, ["administrator"]));
    await model.LoadAsync(token); await model.RevokeAsync(id, token);
    var revokedCommitted = model.Items.Count == 0;
    return IntegrityObservation("revoked", revokedCommitted, revokedCommitted, [], new { strategy = "exact-read", committed = revokedCommitted });
  }

  private static async Task<JsonElement> RunVoteRevocationAsync(
      JsonElement input, CancellationToken token)
  {
    var service = new ModerationIntegrityTestService();
    var id = input.GetProperty("preconditions").GetProperty("penaltyId").GetString()!;
    var active = new VoteWeightPenalty(id, "user", 0.5, "reason", null, "admin", null, null, DateTimeOffset.UtcNow);
    var revoked = active with { RevokedAt = input.GetProperty("serverOutcome").GetProperty("exactReadRevoked").GetBoolean() ? DateTimeOffset.UtcNow : null };
    service.FetchVotePenalties = (_, _, _, _) => Task.FromResult(new VoteIntegrityPenaltiesResponse([active], new PageInfo(null, false, id), new("flag", null)));
    service.RevokeVotePenalty = (_, _) => Task.FromException<VoteWeightPenaltyResponse>(new HttpRequestException("ambiguous", null, HttpStatusCode.InternalServerError));
    service.FetchVotePenalty = (_, _) => Task.FromResult(new VoteWeightPenaltyResponse(revoked));
    var model = new VoteIntegrityPenaltyViewModel(service, new NavigationViewer(true, ["administrator"]));
    await model.LoadAsync(token); await model.RevokeAsync(id, token);
    var revokedCommitted = model.Items.Count == 0;
    return IntegrityObservation("revoked", revokedCommitted, revokedCommitted, [], new { strategy = "exact-read", committed = revokedCommitted });
  }

  private static VoteIntegrityPenaltiesResponse VotePenaltyPage(IReadOnlySet<string> ids, string flagId) =>
      new(ids.Select(id => new VoteWeightPenalty(id, "user", 0.5, "reason", flagId, "admin", null, null, DateTimeOffset.UtcNow)).ToArray(), new PageInfo(null, false, null), new("flag", flagId));

  private sealed record PageItem(string Id);
}

internal sealed record LifecycleScenario(
    string Id,
    string Family,
    IReadOnlySet<string> RequiredConsumers,
    JsonElement Input,
    JsonElement Expected);

internal sealed class LifecycleScenarioContract
{
  private readonly IReadOnlyList<LifecycleScenario> scenarios;
  private readonly IReadOnlyList<(string ScenarioId, string Consumer, string Adapter)> claims;

  private LifecycleScenarioContract(
      IReadOnlyList<LifecycleScenario> scenarios,
      IReadOnlyList<(string ScenarioId, string Consumer, string Adapter)> claims) =>
      (this.scenarios, this.claims) = (scenarios, claims);

  public static LifecycleScenarioContract Load([CallerFilePath] string sourceFile = "")
  {
    var path = FindContract(sourceFile);
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    var root = document.RootElement;
    return new(
        root.GetProperty("scenarios").EnumerateArray().Select(value => new LifecycleScenario(
            value.GetProperty("id").GetString()!, value.GetProperty("family").GetString()!,
            value.GetProperty("requiredConsumers").EnumerateArray()
                .Select(consumer => consumer.GetString()!).ToHashSet(),
            value.GetProperty("input").Clone(), value.GetProperty("expected").Clone())).ToArray(),
        root.GetProperty("claims").EnumerateArray().Select(value => (
            value.GetProperty("scenarioId").GetString()!, value.GetProperty("consumer").GetString()!,
            value.GetProperty("adapter").GetString()!)).ToArray());
  }

  public IReadOnlyList<LifecycleScenario> ScenariosFor(string consumer)
  {
    var claimed = claims.Where(claim => claim.Consumer == consumer).Select(claim => claim.ScenarioId).ToHashSet();
    var required = scenarios.Where(scenario => IsRequired(scenario.Id, consumer)).Select(scenario => scenario.Id).ToHashSet();
    Assert.True(claimed.SetEquals(required), $"{consumer} claim set must equal required scenarios.");
    return scenarios.Where(scenario => claimed.Contains(scenario.Id)).ToArray();
  }

  public string AdapterFor(LifecycleScenario scenario, string consumer)
  {
    var matches = claims.Where(claim => claim.ScenarioId == scenario.Id && claim.Consumer == consumer).ToArray();
    Assert.Single(matches);
    return matches[0].Adapter;
  }

  private bool IsRequired(string scenarioId, string consumer) => scenarios.Single(
      scenario => scenario.Id == scenarioId).RequiredConsumers.Contains(consumer);

  private static string FindContract(string sourceFile)
  {
    foreach (var start in new[] {
        Path.GetDirectoryName(sourceFile) ?? string.Empty,
        AppContext.BaseDirectory,
        Directory.GetCurrentDirectory(),
    })
    {
      for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
      {
        var path = Path.Combine(directory.FullName, "api-fixtures", "v1", "lifecycle-scenarios.json");
        if (File.Exists(path)) return path;
      }
    }
    throw new FileNotFoundException("Could not locate lifecycle-scenarios.json.");
  }
}
