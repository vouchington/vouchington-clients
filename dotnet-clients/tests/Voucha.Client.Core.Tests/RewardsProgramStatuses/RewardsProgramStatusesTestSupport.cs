using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.RewardsProgramStatuses;

namespace Voucha.Client.Core.Tests.RewardsProgramStatuses;

internal sealed class RewardsProgramStatusesTestService : IRewardsProgramStatusesService
{
  public Queue<RewardsProgramStatusPage> Pages { get; } = [];
  public Queue<Exception> FetchErrors { get; } = [];
  public List<string?> Cursors { get; } = [];
  public List<CreateRewardsProgramStatusBody> CreateBodies { get; } = [];
  public List<(string Id, UpdateRewardsProgramStatusBody Body)> UpdateBodies { get; } = [];
  public List<string> DeletedIds { get; } = [];
  public Task<RewardsProgramStatusPage>? FetchPending { get; set; }
  public Task<RewardsProgramStatus>? CreatePending { get; set; }
  public Task<RewardsProgramStatus>? UpdatePending { get; set; }
  public Task? DeletePending { get; set; }
  public Task<IReadOnlyList<RewardsProgramStatusOption>>? SearchPending { get; set; }
  public IReadOnlyList<RewardsProgramStatusOption> SearchResults { get; set; } = [];
  public RewardsProgramStatus CreateResult { get; set; } = Status("created");
  public RewardsProgramStatus UpdateResult { get; set; } = Status("updated");

  public Task<RewardsProgramStatusPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
  {
    Cursors.Add(after);
    if (FetchErrors.TryDequeue(out var error)) return Task.FromException<RewardsProgramStatusPage>(error);
    if (FetchPending is { } pending) { FetchPending = null; return pending; }
    return Task.FromResult(Pages.Dequeue());
  }

  public Task<RewardsProgramStatus> CreateAsync(CreateRewardsProgramStatusBody body, CancellationToken cancellationToken = default)
  {
    CreateBodies.Add(body);
    return CreatePending ?? Task.FromResult(CreateResult);
  }

  public Task<RewardsProgramStatus> UpdateAsync(string id, UpdateRewardsProgramStatusBody body, CancellationToken cancellationToken = default)
  {
    UpdateBodies.Add((id, body));
    return UpdatePending ?? Task.FromResult(UpdateResult with { Id = id });
  }

  public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
  {
    DeletedIds.Add(id);
    return DeletePending ?? Task.CompletedTask;
  }

  public Task<IReadOnlyList<RewardsProgramStatusOption>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
      SearchPending ?? Task.FromResult(SearchResults);

  public static RewardsProgramStatus Status(string id, DateOnly? since = null, DateOnly? until = null) =>
      new(id, "topic-" + id, new("program-" + id, "Program " + id, "program-" + id), since, until);
  public static RewardsProgramStatusPage Page(bool more, string? cursor, params RewardsProgramStatus[] values) =>
      new(values, new(cursor, more, null));
  public static RewardsProgramStatusOptionRow Option(string id = "topic") =>
      new(new(id, "Topic " + id, "topic-" + id, "rewards_program_status"), UiLocalization.English);
}
