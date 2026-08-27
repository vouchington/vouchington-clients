using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.RewardsProgramStatuses;
using Xunit;

namespace Voucha.Client.Core.Tests.RewardsProgramStatuses;

public sealed class RewardsProgramStatusesViewModelTests
{
  [Fact]
  public async Task AppendsDeduplicatesAndForwardsCursor()
  {
    var service = new Service(Page(true, "next", Status("a")));
    var model = new RewardsProgramStatusesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.Pages.Enqueue(Page(false, null, Status("a", since: new(2024, 2, 1)), Status("b")));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal("next", service.Cursors[1]);
    Assert.Equal(["a", "b"], model.Statuses.Select(x => x.Id));
    Assert.Equal(new DateOnly(2024, 2, 1), model.Statuses[0].Since);
  }

  [Fact]
  public async Task RepeatedTopicCreatesAreAllowedAndDeleteRollsBack()
  {
    var service = new Service(Page(false, null, Status("a"))) { CreateResult = Status("created") };
    var model = new RewardsProgramStatusesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var option = new RewardsProgramStatusOptionRow(new("topic", "Topic", "topic", "rewards_program_status"), UiLocalization.English);
    await model.CreateAsync(option, TestContext.Current.CancellationToken);
    await model.CreateAsync(option, TestContext.Current.CancellationToken);
    Assert.Equal(2, service.Creates);
    var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.DeleteTask = pending.Task;
    var deleting = model.DeleteAsync(model.Rows.Single(x => x.Id == "a"), TestContext.Current.CancellationToken);
    Assert.DoesNotContain(model.Statuses, x => x.Id == "a");
    pending.SetException(new InvalidOperationException("offline"));
    await deleting;
    Assert.Contains(model.Statuses, x => x.Id == "a");
  }

  [Fact]
  public async Task NoOpDateSaveSkipsPatchAndSearchFiltersWrongTypes()
  {
    var service = new Service(Page(false, null, Status("a", since: new(2024, 1, 1))));
    var model = new RewardsProgramStatusesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.BeginEdit(model.Rows.Single());
    await model.SaveAsync(TestContext.Current.CancellationToken);
    Assert.Equal(0, service.Updates);
    model.SearchQuery = "status";
    service.Results = [new("wrong", "Wrong", "wrong", "card"), new("ok", "Okay", "okay", "rewards_program_status")];
    await model.SearchAsync(TestContext.Current.CancellationToken);
    Assert.Single(model.SearchRows);
  }

  private static RewardsProgramStatus Status(string id, DateOnly? since = null) =>
      new(id, "topic", new RewardsProgramSummary("program", "Program", "program"), since, null);
  private static RewardsProgramStatusPage Page(bool more, string? cursor, params RewardsProgramStatus[] values) => new(values, new(cursor, more, null));

  private sealed class Service(params RewardsProgramStatusPage[] pages) : IRewardsProgramStatusesService
  {
    public Queue<RewardsProgramStatusPage> Pages { get; } = new(pages);
    public List<string?> Cursors { get; } = [];
    public IReadOnlyList<RewardsProgramStatusOption> Results { get; set; } = [];
    public RewardsProgramStatus CreateResult { get; set; } = Status("created");
    public Task? DeleteTask { get; set; }
    public int Creates { get; private set; }
    public int Updates { get; private set; }
    public Task<RewardsProgramStatusPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    { Cursors.Add(after); return Task.FromResult(Pages.Dequeue()); }
    public Task<RewardsProgramStatus> CreateAsync(CreateRewardsProgramStatusBody body, CancellationToken cancellationToken = default)
    { Creates++; return Task.FromResult(CreateResult); }
    public Task<RewardsProgramStatus> UpdateAsync(string id, UpdateRewardsProgramStatusBody body, CancellationToken cancellationToken = default)
    { Updates++; return Task.FromResult(Status(id)); }
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => DeleteTask ?? Task.CompletedTask;
    public Task<IReadOnlyList<RewardsProgramStatusOption>> SearchAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult(Results);
  }
}
