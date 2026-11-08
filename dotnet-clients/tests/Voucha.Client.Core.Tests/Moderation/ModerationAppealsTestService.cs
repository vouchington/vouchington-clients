using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Tests.Api;

namespace Voucha.Client.Core.Tests.Moderation;

internal sealed class ModerationAppealsTestService : IModerationAppealsService
{
  private readonly ModerationAppeal fixtureAppeal = JsonSerializer
      .Deserialize<ModerationAppealResponse>(
          ApiFixtureLoader.LoadResponse("native.moderation.appeals.detail.default"),
          VouchaApiJson.Options)!
      .Appeal;

  public List<string> Calls { get; } = [];
  public Queue<StaffModerationAppealListResponse> SummaryPages { get; } = new();
  public Queue<StaffModerationAppealListResponse> Pages { get; } = [];
  public Queue<ModerationAppeal> Details { get; } = [];
  public Exception? NextError { get; set; }
  public TaskCompletionSource<ModerationAppealResponse>? PendingDelivery { get; set; }
  public TaskCompletionSource<ModerationAppealResponse>? PendingApproval { get; set; }
  public TaskCompletionSource<ModerationAppealResponse>? PendingDetail { get; set; }
  public TaskCompletionSource<StaffModerationAppealListResponse>? PendingFetch { get; set; }
  public ModerationAppeal Current { get; set; }
  public bool QueueAccepted { get; set; } = true;

  public ModerationAppealsTestService() => Current = fixtureAppeal;

  public ModerationAppeal Appeal(
      string id = "appeal-1",
      ModerationAppealStatus status = ModerationAppealStatus.Pending,
      string? response = null,
      bool approved = false,
      bool suspension = false) => fixtureAppeal with
      {
        Id = id,
        Status = status,
        PublicResponse = response,
        ApprovedAt = approved ? DateTimeOffset.UtcNow : null,
        UserSuspensionId = suspension ? "suspension-1" : null,
      };

  public async Task<StaffModerationAppealListResponse> FetchAppealsAsync(
      ModerationAppealStatus status, string? after = null, int limit = 25,
      CancellationToken cancellationToken = default)
  {
    if (SummaryPages.TryDequeue(out var summaryPage)) return summaryPage;
    return await FetchPageAsync(status, after, limit, cancellationToken).ConfigureAwait(false);
  }

  private Task<StaffModerationAppealListResponse> FetchPageAsync(
      ModerationAppealStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default)
  {
    Calls.Add($"fetch:{status}:{after}");
    ThrowIfNeeded();
    if (PendingFetch is { } pending)
    {
      PendingFetch = null;
      return pending.Task;
    }
    if (Pages.TryDequeue(out var page)) return Task.FromResult(page);
    return Task.FromResult(new StaffModerationAppealListResponse([Summary(Current)], new PageInfo(null, false, null)));
  }

  public static ModerationAppealSummary Summary(ModerationAppeal appeal) =>
      JsonSerializer.Deserialize<ModerationAppealSummary>(
          JsonSerializer.Serialize(appeal, VouchaApiJson.Options), VouchaApiJson.Options)!;

  public Task<ModerationAppealResponse> FetchAppealAsync(string id, CancellationToken cancellationToken = default)
  {
    Calls.Add($"detail:{id}");
    ThrowIfNeeded();
    if (PendingDetail is { } pending)
    {
      PendingDetail = null;
      return pending.Task;
    }
    return Task.FromResult(new ModerationAppealResponse(
        Details.TryDequeue(out var detail) ? detail : Current));
  }

  public Task<ModerationAppealResponse> UpdatePublicResponseAsync(string id, string publicResponse, CancellationToken cancellationToken = default)
  {
    Calls.Add($"update:{id}:{publicResponse}");
    ThrowIfNeeded();
    Current = Current with { PublicResponse = publicResponse, EditedAt = DateTimeOffset.UtcNow };
    return Task.FromResult(new ModerationAppealResponse(Current));
  }

  public Task<ModerationAppealResponse> ApproveAsync(string id, CancellationToken cancellationToken = default)
  {
    Calls.Add($"approve:{id}");
    ThrowIfNeeded();
    if (PendingApproval is { } pending) return pending.Task;
    Current = Current with { ApprovedAt = DateTimeOffset.UtcNow };
    return Task.FromResult(new ModerationAppealResponse(Current));
  }

  public Task<ModerationAppealResponse> DeliverAsync(string id, CancellationToken cancellationToken = default)
  {
    Calls.Add($"deliver:{id}");
    ThrowIfNeeded();
    if (PendingDelivery is { } pending) return pending.Task;
    Current = Current with { SentAt = DateTimeOffset.UtcNow };
    return Task.FromResult(new ModerationAppealResponse(Current));
  }

  public Task<ModerationAppealResponse> ResolveAsync(string id, ModerationAppealAction action, CancellationToken cancellationToken = default)
  {
    Calls.Add($"resolve:{id}:{action}");
    ThrowIfNeeded();
    Current = Current with
    {
      Status = action == ModerationAppealAction.Deny ? ModerationAppealStatus.Dismissed : ModerationAppealStatus.Resolved,
      ResolutionAction = action,
    };
    return Task.FromResult(new ModerationAppealResponse(Current));
  }

  public Task<ModerationAppealQueueResponse> RerunResolutionDraftAsync(string id, CancellationToken cancellationToken = default)
  {
    Calls.Add($"rerun:{id}");
    ThrowIfNeeded();
    var admission = JsonSerializer.Deserialize<ModerationAppealQueueResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.appeals.resolution-drafts.default"), VouchaApiJson.Options)!;
    return Task.FromResult(admission with { Queued = QueueAccepted, RerunById = "staff-1" });
  }

  private void ThrowIfNeeded()
  {
    if (NextError is not { } error) return;
    NextError = null;
    throw error;
  }
}
