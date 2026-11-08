using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Tests.Api;

namespace Voucha.Client.App.Tests;

internal abstract class ModerationModalActionService
{
  public string Mode { get; set; } = "normal";
  public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
  public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

  protected async Task<T> RespondAsync<T>(T response, CancellationToken token)
  {
    if (Mode == "reject") throw new HttpRequestException("The server rejected this edited response.", null, HttpStatusCode.BadRequest);
    if (Mode != "block") return response;
    Started.TrySetResult();
    try
    {
      return await new TaskCompletionSource<T>().Task.WaitAsync(token);
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested)
    {
      CancellationObserved.TrySetResult();
      throw;
    }
  }
}

internal sealed class ModerationModalAppealsService : ModerationModalActionService, IModerationAppealsService
{
  public ModerationAppeal Current { get; set; } = JsonSerializer.Deserialize<ModerationAppealResponse>(
      File.ReadAllText(FilamentsContractPaths.ApiFixture("responses/native.moderation.appeals.detail.default.json")), VouchaApiJson.Options)!.Appeal;

  public Queue<StaffModerationAppealListResponse> Pages { get; } = new();
  public List<string?> Cursors { get; } = [];

  public Task<StaffModerationAppealListResponse> FetchAppealsAsync(ModerationAppealStatus status,
      string? after = null, int limit = 25, CancellationToken cancellationToken = default)
  {
    Cursors.Add(after);
    return Task.FromResult(Pages.Count > 0 ? Pages.Dequeue() : new StaffModerationAppealListResponse([], new PageInfo(null, false, null)));
  }

  public int DetailFetches { get; private set; }

  public Task<ModerationAppealResponse> FetchAppealAsync(string id, CancellationToken cancellationToken = default)
  {
    DetailFetches++;
    return Mode is "seed-ai" or "seed-delivery"
        ? Task.FromException<ModerationAppealResponse>(new HttpRequestException("Refresh is temporarily unavailable."))
        : RespondAsync(new ModerationAppealResponse(Current), cancellationToken);
  }

  public Task<ModerationAppealResponse> UpdatePublicResponseAsync(string id, string response, CancellationToken cancellationToken = default) =>
      RespondAsync(new ModerationAppealResponse(Current with { PublicResponse = response }), cancellationToken);

  public Task<ModerationAppealResponse> ApproveAsync(string id, CancellationToken cancellationToken = default) =>
      RespondAsync(new ModerationAppealResponse(Current), cancellationToken);

  public Task<ModerationAppealResponse> DeliverAsync(string id, CancellationToken cancellationToken = default) =>
      Mode == "seed-delivery"
          ? Task.FromException<ModerationAppealResponse>(new HttpRequestException("Delivery outcome is unknown.", null, HttpStatusCode.BadGateway))
          : RespondAsync(new ModerationAppealResponse(Current), cancellationToken);

  public Task<ModerationAppealQueueResponse> RerunResolutionDraftAsync(string id, CancellationToken cancellationToken = default) =>
      RespondAsync(new ModerationAppealQueueResponse(true, "staff-1", "appeal_resolution_00000000-0000-7000-8000-000000000299"), cancellationToken);

  public Task<ModerationAppealResponse> ResolveAsync(string id, ModerationAppealAction action, CancellationToken cancellationToken = default) =>
      RespondAsync(new ModerationAppealResponse(Current), cancellationToken);
}

internal sealed class ModerationModalDisputesService : ModerationModalActionService, IModerationDisputesService
{
  public ModerationDispute Current { get; set; } = JsonSerializer.Deserialize<ModerationDisputeResponse>(
      File.ReadAllText(FilamentsContractPaths.ApiFixture("responses/native.moderation.disputes.detail.default.json")), VouchaApiJson.Options)!.Dispute;

  public Queue<StaffModerationDisputeListResponse> Pages { get; } = new();
  public List<string?> Cursors { get; } = [];

  public Task<StaffModerationDisputeListResponse> FetchDisputesAsync(ModerationDisputeStatus status,
      string? after = null, int limit = 25, CancellationToken cancellationToken = default)
  {
    Cursors.Add(after);
    return Task.FromResult(Pages.Count > 0 ? Pages.Dequeue() : new StaffModerationDisputeListResponse([], new PageInfo(null, false, null)));
  }

  public Task<ModerationDisputeResponse> FetchDisputeAsync(string id, CancellationToken cancellationToken = default) =>
      RespondAsync(new ModerationDisputeResponse(Current), cancellationToken);

  public Task<ModerationDisputeResponse> UpdatePublicResponseAsync(string id, string response, CancellationToken cancellationToken = default) =>
      RespondAsync(new ModerationDisputeResponse(Current with { PublicResponse = response }), cancellationToken);

  public Task<ModerationDisputeResponse> ApproveAsync(string id, CancellationToken cancellationToken = default) =>
      RespondAsync(new ModerationDisputeResponse(Current), cancellationToken);

  public Task<ModerationDisputeResponse> DeliverAsync(string id, CancellationToken cancellationToken = default) =>
      RespondAsync(new ModerationDisputeResponse(Current), cancellationToken);

  public Task<ModerationDisputeQueueResponse> RerunResolutionDraftAsync(string id, CancellationToken cancellationToken = default) =>
      RespondAsync(new ModerationDisputeQueueResponse(true, "staff-1", "dispute_resolution_00000000-0000-7000-8000-000000000299"), cancellationToken);

  public Task<ModerationDisputeResponse> ResolveAsync(string id, ModerationDisputeResolutionAction action, string? annotation,
      CancellationToken cancellationToken = default) => RespondAsync(new ModerationDisputeResponse(Current), cancellationToken);
}
