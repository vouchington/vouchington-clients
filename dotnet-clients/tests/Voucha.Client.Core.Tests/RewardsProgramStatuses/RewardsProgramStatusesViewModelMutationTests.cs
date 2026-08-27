using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.RewardsProgramStatuses;
using Xunit;

namespace Voucha.Client.Core.Tests.RewardsProgramStatuses;

public sealed class RewardsProgramStatusesViewModelMutationTests
{
  [Fact]
  public async Task ConcurrentCreateIsSuppressedWhileSequentialRepeatedTopicsRemainAllowed()
  {
    var service = LoadedService(RewardsProgramStatusesTestService.Status("a"));
    var pending = new TaskCompletionSource<RewardsProgramStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.CreatePending = pending.Task;
    var model = await Load(service);
    var first = model.CreateAsync(RewardsProgramStatusesTestService.Option(), TestContext.Current.CancellationToken);
    var duplicate = model.CreateAsync(RewardsProgramStatusesTestService.Option(), TestContext.Current.CancellationToken);
    Assert.Single(service.CreateBodies);
    pending.SetResult(RewardsProgramStatusesTestService.Status("created-1"));
    await Task.WhenAll(first, duplicate);

    service.CreatePending = null;
    service.CreateResult = RewardsProgramStatusesTestService.Status("created-2");
    await model.CreateAsync(RewardsProgramStatusesTestService.Option(), TestContext.Current.CancellationToken);
    Assert.Equal(2, service.CreateBodies.Count);
  }

  [Fact]
  public async Task PendingCreateAndSaveExposeDisabledStateAndSuppressRepeatedInvocations()
  {
    var service = LoadedService(RewardsProgramStatusesTestService.Status("a"));
    var createPending = new TaskCompletionSource<RewardsProgramStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.CreatePending = createPending.Task;
    var model = await Load(service);

    var create = model.CreateAsync(RewardsProgramStatusesTestService.Option(), TestContext.Current.CancellationToken);
    var duplicateCreate = model.CreateAsync(RewardsProgramStatusesTestService.Option(), TestContext.Current.CancellationToken);
    Assert.False(model.CanCreate);
    Assert.Single(service.CreateBodies);
    createPending.SetResult(RewardsProgramStatusesTestService.Status("created"));
    await Task.WhenAll(create, duplicateCreate);

    var updatePending = new TaskCompletionSource<RewardsProgramStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.UpdatePending = updatePending.Task;
    model.BeginEdit(model.Rows.Single(row => row.Id == "a"));
    model.EditDraft!.SinceEnabled = true;
    model.EditDraft.Since = "2024-01-01";
    var save = model.SaveAsync(TestContext.Current.CancellationToken);
    var duplicateSave = model.SaveAsync(TestContext.Current.CancellationToken);
    Assert.False(model.CanSave);
    Assert.Single(service.UpdateBodies);
    updatePending.SetResult(RewardsProgramStatusesTestService.Status("a", new(2024, 1, 1)));
    await Task.WhenAll(save, duplicateSave);
    Assert.True(model.CanCreate);
  }

  [Fact]
  public async Task SameRowMutationIsSuppressedWhileDifferentRowsMayMutate()
  {
    var service = LoadedService(RewardsProgramStatusesTestService.Status("a"), RewardsProgramStatusesTestService.Status("b"));
    var firstUpdate = new TaskCompletionSource<RewardsProgramStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.UpdatePending = firstUpdate.Task;
    var model = await Load(service);
    model.BeginEdit(model.Rows.Single(row => row.Id == "a"));
    model.EditDraft!.SinceEnabled = true;
    model.EditDraft.Since = "2024-01-01";
    var update = model.SaveAsync(TestContext.Current.CancellationToken);
    Assert.True(model.IsMutating("a"));

    await model.DeleteAsync(model.Rows.Single(row => row.Id == "a"), TestContext.Current.CancellationToken);
    var deleteB = model.DeleteAsync(model.Rows.Single(row => row.Id == "b"), TestContext.Current.CancellationToken);
    Assert.DoesNotContain("a", service.DeletedIds);
    Assert.Equal(["b"], service.DeletedIds);
    firstUpdate.SetResult(RewardsProgramStatusesTestService.Status("a", new(2024, 1, 1)));
    await Task.WhenAll(update, deleteB);
  }

  [Fact]
  public async Task UpdateSendsFullAndClearDatePayloads()
  {
    var service = LoadedService(RewardsProgramStatusesTestService.Status("a", new(2024, 1, 1), new(2024, 2, 1)));
    var model = await Load(service);
    model.BeginEdit(model.Rows.Single());
    model.EditDraft!.Since = "2024-03-01";
    model.EditDraft.Until = "2024-04-01";
    service.UpdateResult = RewardsProgramStatusesTestService.Status("a", new(2024, 3, 1), new(2024, 4, 1));
    await model.SaveAsync(TestContext.Current.CancellationToken);
    var full = Assert.Single(service.UpdateBodies).Body;
    Assert.Equal(new DateOnly(2024, 3, 1), full.Since!.Value.Value);
    Assert.Equal(new DateOnly(2024, 4, 1), full.Until!.Value.Value);

    model.BeginEdit(model.Rows.Single());
    model.EditDraft!.SinceEnabled = false;
    model.EditDraft.UntilEnabled = false;
    service.UpdateResult = RewardsProgramStatusesTestService.Status("a");
    await model.SaveAsync(TestContext.Current.CancellationToken);
    var cleared = service.UpdateBodies.Last().Body;
    Assert.Null(cleared.Since!.Value.Value);
    Assert.Null(cleared.Until!.Value.Value);
  }

  [Fact]
  public async Task ReversedRangeShowsLocalizedValidationAndSkipsUpdate()
  {
    var service = LoadedService(RewardsProgramStatusesTestService.Status("a"));
    var model = await Load(service);
    model.BeginEdit(model.Rows.Single());
    model.EditDraft!.SinceEnabled = true;
    model.EditDraft.Since = "2024-02-01";
    model.EditDraft.UntilEnabled = true;
    model.EditDraft.Until = "2024-01-01";

    await model.SaveAsync(TestContext.Current.CancellationToken);

    Assert.Empty(service.UpdateBodies);
    Assert.Equal(UiMessageKey.ExtractedMyRewardsProgramStatusesManagerSinceMustBeBeforeUntilA4745364, model.ErrorText!.Value.Key);
    Assert.NotNull(model.EditDraft);
  }

  [Fact]
  public async Task StaleSaveDoesNotCloseNewerEditor()
  {
    var service = LoadedService(RewardsProgramStatusesTestService.Status("a"), RewardsProgramStatusesTestService.Status("b"));
    var pending = new TaskCompletionSource<RewardsProgramStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.UpdatePending = pending.Task;
    var model = await Load(service);
    model.BeginEdit(model.Rows.Single(row => row.Id == "a"));
    model.EditDraft!.SinceEnabled = true;
    model.EditDraft.Since = "2024-01-01";
    var save = model.SaveAsync(TestContext.Current.CancellationToken);
    model.BeginEdit(model.Rows.Single(row => row.Id == "b"));
    var newerDraft = model.EditDraft;
    pending.SetResult(RewardsProgramStatusesTestService.Status("a", new(2024, 1, 1)));
    await save;

    Assert.Same(newerDraft, model.EditDraft);
    Assert.Equal("b", model.EditDraft!.Original!.Id);
  }

  [Fact]
  public async Task DeleteSuccessRemovesTheRowAndFailureRollsItBack()
  {
    var service = LoadedService(RewardsProgramStatusesTestService.Status("a"), RewardsProgramStatusesTestService.Status("b"));
    var model = await Load(service);
    await model.DeleteAsync(model.Rows.Single(row => row.Id == "a"), TestContext.Current.CancellationToken);
    Assert.Equal(["b"], model.Statuses.Select(value => value.Id));

    service.Pages.Enqueue(RewardsProgramStatusesTestService.Page(false, null, RewardsProgramStatusesTestService.Status("a"), RewardsProgramStatusesTestService.Status("b")));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.DeletePending = pending.Task;
    var deleting = model.DeleteAsync(model.Rows.Single(row => row.Id == "a"), TestContext.Current.CancellationToken);
    Assert.DoesNotContain(model.Statuses, value => value.Id == "a");
    pending.SetException(new InvalidOperationException("offline"));
    await deleting;
    Assert.Equal(["a", "b"], model.Statuses.Select(value => value.Id));
  }

  [Fact]
  public async Task RefreshDuringPendingMutationsPreservesLocalRowsUntilReconciliation()
  {
    var service = LoadedService(RewardsProgramStatusesTestService.Status("a"), RewardsProgramStatusesTestService.Status("b"));
    var model = await Load(service);

    var staleCreateRead = new TaskCompletionSource<RewardsProgramStatusPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.FetchPending = staleCreateRead.Task;
    var createRefresh = model.LoadAsync(TestContext.Current.CancellationToken);
    service.CreateResult = RewardsProgramStatusesTestService.Status("created");
    await model.CreateAsync(RewardsProgramStatusesTestService.Option(), TestContext.Current.CancellationToken);
    staleCreateRead.SetResult(RewardsProgramStatusesTestService.Page(false, null, RewardsProgramStatusesTestService.Status("a"), RewardsProgramStatusesTestService.Status("b")));
    await createRefresh;
    Assert.Contains(model.Statuses, value => value.Id == "created");

    var staleUpdateRead = new TaskCompletionSource<RewardsProgramStatusPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.FetchPending = staleUpdateRead.Task;
    var updateRefresh = model.LoadAsync(TestContext.Current.CancellationToken);
    model.BeginEdit(model.Rows.Single(row => row.Id == "a"));
    model.EditDraft!.SinceEnabled = true;
    model.EditDraft.Since = "2024-05-01";
    service.UpdateResult = RewardsProgramStatusesTestService.Status("a", new(2024, 5, 1));
    await model.SaveAsync(TestContext.Current.CancellationToken);
    staleUpdateRead.SetResult(RewardsProgramStatusesTestService.Page(false, null, RewardsProgramStatusesTestService.Status("a"), RewardsProgramStatusesTestService.Status("b")));
    await updateRefresh;
    Assert.Equal(new DateOnly(2024, 5, 1), model.Statuses.Single(value => value.Id == "a").Since);

    var staleDeleteRead = new TaskCompletionSource<RewardsProgramStatusPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.FetchPending = staleDeleteRead.Task;
    var deleteRefresh = model.LoadAsync(TestContext.Current.CancellationToken);
    await model.DeleteAsync(model.Rows.Single(row => row.Id == "b"), TestContext.Current.CancellationToken);
    staleDeleteRead.SetResult(RewardsProgramStatusesTestService.Page(false, null, RewardsProgramStatusesTestService.Status("a"), RewardsProgramStatusesTestService.Status("b")));
    await deleteRefresh;
    Assert.DoesNotContain(model.Statuses, value => value.Id == "b");

    service.Pages.Enqueue(RewardsProgramStatusesTestService.Page(false, null, RewardsProgramStatusesTestService.Status("a", new(2024, 6, 1))));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["a"], model.Statuses.Select(value => value.Id));
    Assert.Equal(new DateOnly(2024, 6, 1), model.Statuses.Single().Since);
  }

  private static RewardsProgramStatusesTestService LoadedService(params RewardsProgramStatus[] values)
  {
    var service = new RewardsProgramStatusesTestService();
    service.Pages.Enqueue(RewardsProgramStatusesTestService.Page(false, null, values));
    return service;
  }

  private static async Task<RewardsProgramStatusesViewModel> Load(RewardsProgramStatusesTestService service)
  {
    var model = new RewardsProgramStatusesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    return model;
  }
}
