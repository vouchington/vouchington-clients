using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Tests.Api;

namespace Voucha.Client.Core.Tests.Moderation;

internal sealed class ModerationAppealsTestService : IModerationAppealsService
{
  private readonly ModerationAppeal fixtureAppeal = JsonSerializer
      .Deserialize<ModerationAppealListResponse>(
          ApiFixtureLoader.LoadResponse("native.moderation.appeals.default"),
          VouchaApiJson.Options)!
      .Appeals[0];

  public List<string> Calls { get; } = [];
  public Queue<ModerationAppealListResponse> Pages { get; } = [];
  public Queue<ModerationAppeal> Details { get; } = [];
  public Exception? NextError { get; set; }
  public TaskCompletionSource<ModerationAppealResponse>? PendingDelivery { get; set; }
  public TaskCompletionSource<ModerationAppealListResponse>? PendingFetch { get; set; }
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

  public Task<ModerationAppealListResponse> FetchAppealsAsync(
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
    return Task.FromResult(new ModerationAppealListResponse([Current], new PageInfo(null, false, null)));
  }

  public Task<ModerationAppealResponse> FetchAppealAsync(string id, CancellationToken cancellationToken = default)
  {
    Calls.Add($"detail:{id}");
    ThrowIfNeeded();
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
    return Task.FromResult(new ModerationAppealQueueResponse(QueueAccepted, "staff-1"));
  }

  private void ThrowIfNeeded()
  {
    if (NextError is not { } error) return;
    NextError = null;
    throw error;
  }
}
