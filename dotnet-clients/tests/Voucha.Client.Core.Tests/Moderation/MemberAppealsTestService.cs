using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.Core.Tests.Moderation;

internal sealed class MemberAppealsTestService : IMemberAppealsService
{
  public List<string> Calls { get; } = [];
  public Dictionary<ModerationAppealStatus, Queue<ModerationAppealListResponse>> AppealPages { get; } =
      Enum.GetValues<ModerationAppealStatus>().ToDictionary(status => status, _ => new Queue<ModerationAppealListResponse>());
  public Queue<MemberWarningNoticesResponse> WarningPages { get; } = [];
  public Queue<PersonalCommunityBansResponse> BanPages { get; } = [];
  public Queue<PersonalRemovedPostsResponse> RemovalPages { get; } = [];
  public MyIdentityResponse Identity { get; set; } = new(new User("user-1", "member"));
  public ModerationAppealSubmissionResponse Submission { get; set; } =
      new(MemberAppealsFixtures.Appeal("submitted"), false);
  public Exception? SubmissionError { get; set; }
  public TaskCompletionSource<ModerationAppealSubmissionResponse>? PendingSubmission { get; set; }
  public TaskCompletionSource<ModerationAppealListResponse>? PendingAppealFetch { get; set; }
  public ModerationAppealSubmissionRequest? LastSubmissionRequest { get; private set; }
  public Dictionary<ModerationAppealStatus, Exception> AppealErrors { get; } = [];
  public Exception? WarningError { get; set; }

  public Task<ModerationAppealListResponse> FetchAppealsAsync(
      ModerationAppealStatus status,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default)
  {
    Calls.Add($"appeals:{status}:{after}");
    if (status == ModerationAppealStatus.Pending && PendingAppealFetch is { } pendingFetch)
    {
      PendingAppealFetch = null;
      return pendingFetch.Task.WaitAsync(cancellationToken);
    }
    if (AppealErrors.Remove(status, out var error))
      return Task.FromException<ModerationAppealListResponse>(error);
    return Task.FromResult(AppealPages[status].TryDequeue(out var page)
        ? page
        : new ModerationAppealListResponse([], new PageInfo(null, false, null)));
  }

  public Task<MemberWarningNoticesResponse> FetchWarningsAsync(
      string? after = null, int limit = 25, CancellationToken cancellationToken = default)
  {
    Calls.Add($"warnings:{after}");
    if (WarningError is { } error)
    {
      WarningError = null;
      return Task.FromException<MemberWarningNoticesResponse>(error);
    }
    return Task.FromResult(WarningPages.TryDequeue(out var page)
        ? page
        : new MemberWarningNoticesResponse([], new PageInfo(null, false, null)));
  }

  public Task<PersonalCommunityBansResponse> FetchBansAsync(
      string? after = null, int limit = 25, CancellationToken cancellationToken = default)
  {
    Calls.Add($"bans:{after}");
    return Task.FromResult(BanPages.TryDequeue(out var page)
        ? page
        : new PersonalCommunityBansResponse([], new PageInfo(null, false, null)));
  }

  public Task<PersonalRemovedPostsResponse> FetchRemovedPostsAsync(
      string? after = null, int limit = 25, CancellationToken cancellationToken = default)
  {
    Calls.Add($"removals:{after}");
    return Task.FromResult(RemovalPages.TryDequeue(out var page)
        ? page
        : new PersonalRemovedPostsResponse([], new PageInfo(null, false, null)));
  }

  public Task<MyIdentityResponse> FetchIdentityAsync(CancellationToken cancellationToken = default)
  {
    Calls.Add("identity");
    return Task.FromResult(Identity);
  }

  public Task<ModerationAppealSubmissionResponse> SubmitAsync(
      ModerationAppealSubmissionRequest request,
      CancellationToken cancellationToken = default)
  {
    Calls.Add($"submit:{request.TargetType}:{request.TargetId}:{request.TurnstileToken}");
    LastSubmissionRequest = request;
    if (SubmissionError is { } error)
    {
      SubmissionError = null;
      return Task.FromException<ModerationAppealSubmissionResponse>(error);
    }
    return PendingSubmission?.Task.WaitAsync(cancellationToken) ?? Task.FromResult(Submission);
  }
}

internal static class MemberAppealsFixtures
{
  public static ModerationAppeal Appeal(
      string id,
      ModerationAppealStatus status = ModerationAppealStatus.Pending,
      string? warningId = null,
      string? banId = null,
      string? postId = null,
      ModerationAppealPostRemovalKind? kind = null,
      string? suspensionId = null) =>
      new(
          id, $"case-{id}", "user-1", warningId, banId, postId, null, kind, "reason", status,
          null, null, null, null, null, null, null, null, null, null, null, null, null, null,
          null, null, null, DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
          DateTimeOffset.Parse("2026-07-01T00:00:00Z"), false, suspensionId);

  public static PageInfo Page(string? cursor = null, bool more = false) =>
      new(cursor, more, null);
}
