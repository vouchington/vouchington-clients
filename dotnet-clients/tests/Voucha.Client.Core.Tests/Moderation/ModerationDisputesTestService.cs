using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Tests.Api;

namespace Voucha.Client.Core.Tests.Moderation;

internal sealed class ModerationDisputesTestService : IModerationDisputesService
{
  public Queue<ModerationDisputeListResponse> Pages { get; } = new();
  public List<string> Calls { get; } = [];
  public ModerationDispute Current { get; set; } = Fixture();
  public Exception? NextError { get; set; }
  public Exception? NextResolveResponseError { get; set; }
  public bool QueueAccepted { get; set; } = true;
  public Queue<ModerationDispute> Refreshes { get; } = new();
  public TaskCompletionSource<ModerationDisputeListResponse>? PendingFetch { get; set; }
  public TaskCompletionSource<ModerationDisputeResponse>? PendingResolve { get; set; }
  public TaskCompletionSource PendingFetchStarted { get; } =
      new(TaskCreationOptions.RunContinuationsAsynchronously);
  public TaskCompletionSource ResolveStarted { get; } =
      new(TaskCreationOptions.RunContinuationsAsynchronously);

  public Task<ModerationDisputeListResponse> FetchDisputesAsync(
      ModerationDisputeStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default)
  {
    Calls.Add($"list:{status}:{after ?? "-"}");
    ThrowNext();
    if (PendingFetch is { } pending)
    {
      PendingFetch = null;
      PendingFetchStarted.TrySetResult();
      return pending.Task;
    }
    return Task.FromResult(Pages.TryDequeue(out var page)
        ? page
        : new([Current with { Status = status }], new PageInfo(null, false, null)));
  }

  public Task<ModerationDisputeResponse> FetchDisputeAsync(
      string id,
      CancellationToken cancellationToken = default)
  {
    Calls.Add($"fetch:{id}");
    ThrowNext();
    if (Refreshes.TryDequeue(out var refreshed)) Current = refreshed;
    return Task.FromResult(new ModerationDisputeResponse(Current));
  }

  public Task<ModerationDisputeResponse> UpdatePublicResponseAsync(
      string id,
      string publicResponse,
      CancellationToken cancellationToken = default)
  {
    Calls.Add($"update:{id}:{publicResponse}");
    ThrowNext();
    Current = Current with { PublicResponse = publicResponse, UpdatedAt = DateTimeOffset.UtcNow };
    return Task.FromResult(new ModerationDisputeResponse(Current));
  }

  public Task<ModerationDisputeResponse> ApproveAsync(
      string id,
      CancellationToken cancellationToken = default)
  {
    Calls.Add($"approve:{id}");
    ThrowNext();
    Current = Current with { ApprovedAt = DateTimeOffset.UtcNow };
    return Task.FromResult(new ModerationDisputeResponse(Current));
  }

  public Task<ModerationDisputeResponse> DeliverAsync(
      string id,
      CancellationToken cancellationToken = default)
  {
    Calls.Add($"deliver:{id}");
    ThrowNext();
    Current = Current with { SentAt = DateTimeOffset.UtcNow };
    return Task.FromResult(new ModerationDisputeResponse(Current));
  }

  public Task<ModerationDisputeResponse> ResolveAsync(
      string id,
      ModerationDisputeResolutionAction action,
      string? annotation,
      CancellationToken cancellationToken = default)
  {
    Calls.Add($"resolve:{id}:{action}:{annotation ?? "-"}");
    ThrowNext();
    Current = Current with
    {
      Status = action == ModerationDisputeResolutionAction.Dismiss
          ? ModerationDisputeStatus.Dismissed
          : ModerationDisputeStatus.Resolved,
      ResolutionAction = action.ToString().ToLowerInvariant(),
      ResolvedAt = DateTimeOffset.UtcNow,
    };
    if (PendingResolve is { } pending)
    {
      PendingResolve = null;
      ResolveStarted.TrySetResult();
      return pending.Task;
    }
    if (NextResolveResponseError is { } responseError)
    {
      NextResolveResponseError = null;
      throw responseError;
    }
    return Task.FromResult(new ModerationDisputeResponse(Current));
  }

  public Task<ModerationDisputeQueueResponse> RerunResolutionDraftAsync(
      string id,
      CancellationToken cancellationToken = default)
  {
    Calls.Add($"rerun:{id}");
    ThrowNext();
    return Task.FromResult(new ModerationDisputeQueueResponse(QueueAccepted, "staff-1"));
  }

  public void FailNext(Exception exception) => NextError = exception;

  private void ThrowNext()
  {
    if (NextError is not { } error) return;
    NextError = null;
    throw error;
  }

  internal static ModerationDispute Fixture()
  {
    var json = ApiFixtureLoader.LoadResponse("native.moderation.disputes.default");
    return JsonSerializer.Deserialize<ModerationDisputeListResponse>(json, VouchaApiJson.Options)!
        .Disputes.Single();
  }

  internal static HttpRequestException AmbiguousFailure() =>
      new("gateway failure", null, HttpStatusCode.BadGateway);
}
