using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.RewardsProgramStatuses;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class RewardsProgramStatusesPageTests
{
  [Fact]
  public async Task AddAndSaveControlsDisableWhileMutationsArePending()
  {
    var service = new Service(Status("pending"));
    var model = new RewardsProgramStatusesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(model);
    var row = CreateRow(page, model.Rows.Single());

    model.SearchQuery = "Gold";
    await model.SearchAsync(TestContext.Current.CancellationToken);
    var option = CreateOptionRow(page, model.SearchRows.Single());
    var createPending = new TaskCompletionSource<RewardsProgramStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.CreatePending = createPending.Task;
    Find<Button>(option, "reward-statuses-create").SendClicked();

    await WaitUntilAsync(() => model.IsCreating);
    Assert.False(Find<Button>(option, "reward-statuses-create").IsEnabled);
    createPending.SetResult(Status("created"));
    await WaitUntilAsync(() => !model.IsCreating);

    Find<Button>(row, "reward-statuses-edit").SendClicked();
    model.EditDraft!.SinceEnabled = true;
    model.EditDraft.Since = "2026-01-02";
    var pending = new TaskCompletionSource<RewardsProgramStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.UpdatePending = pending.Task;
    var save = model.SaveAsync(TestContext.Current.CancellationToken);

    await WaitUntilAsync(() => model.IsMutating("pending"));
    Assert.False(Find<Button>(page, "reward-statuses-save").IsEnabled);
    pending.SetResult(Status("pending"));
    await save;
  }

  [Fact]
  public async Task RendersNativeStatusEditorAndBlocksReversedDates()
  {
    const string id = "6c90acb5-e92f-4336-86e3-05e86cf11b48";
    var service = new Service(Status(id));
    var model = new RewardsProgramStatusesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(model);
    var row = CreateRow(page, model.Rows.Single());

    Assert.Contains(Descendants<Label>(row), label => label.Text == "Gold");
    Assert.DoesNotContain(Descendants<Label>(page).Concat(Descendants<Label>(row)),
        label => label.Text?.Contains(id, StringComparison.Ordinal) == true);
    Assert.Empty(Descendants<WebView>(page));

    model.SearchQuery = "Gold";
    Find<Button>(page, "reward-statuses-search").SendClicked();
    await WaitUntilAsync(() => service.SearchCalls == 1);
    Assert.Single(model.SearchRows);

    Find<Button>(page, "pagination-reward-statuses-action").SendClicked();
    await WaitUntilAsync(() => service.Fetches == 2);
    Assert.Equal([id, "next"], model.Statuses.Select(status => status.Id));

    Find<Button>(row, "reward-statuses-edit").SendClicked();
    Find<Switch>(page, "reward-statuses-edit-since-enabled").IsToggled = true;
    Find<DatePicker>(page, "reward-statuses-edit-since").Date = new DateTime(2026, 2, 1);
    Find<Switch>(page, "reward-statuses-edit-until-enabled").IsToggled = true;
    Find<DatePicker>(page, "reward-statuses-edit-until").Date = new DateTime(2026, 1, 1);
    Find<Button>(page, "reward-statuses-save").SendClicked();

    await WaitUntilAsync(() => model.HasError);
    Assert.Empty(service.UpdateBodies);
    Assert.Contains(Descendants<Label>(page), label => label.Text == UiLocalization.English.Localize(
        UiMessageKey.ExtractedMyRewardsProgramStatusesManagerSinceMustBeBeforeUntilA4745364));

    await page.DeleteConfirmedAsync(model.Rows.Single(status => status.Id == id), TestContext.Current.CancellationToken);
    Assert.Equal([id], service.DeletedIds);
    Assert.DoesNotContain(model.Statuses, status => status.Id == id);
  }

  [Fact]
  public async Task ScrollStopsAfterContinuationFailureUntilExplicitRetry()
  {
    var service = new Service(new RewardsProgramStatusPage(
        [Status("a")], new PageInfo("next", true, null)));
    var model = new RewardsProgramStatusesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(model);
    service.FetchErrors.Enqueue(new InvalidOperationException("offline"));

    await page.LoadMoreForScrollAsync(500, 500, 900, TestContext.Current.CancellationToken);
    Assert.True(model.HasContinuationError);
    Assert.Equal(2, service.Fetches);

    service.Pages.Enqueue(new RewardsProgramStatusPage([Status("b")], new PageInfo(null, false, null)));
    await page.LoadMoreForScrollAsync(500, 500, 900, TestContext.Current.CancellationToken);
    Assert.Equal(2, service.Fetches);

    await page.RetryAsync(TestContext.Current.CancellationToken);
    Assert.Equal(3, service.Fetches);
    Assert.Equal(["a", "b"], model.Statuses.Select(status => status.Id));
  }

  private static RewardsProgramStatusesPage CreatePage(RewardsProgramStatusesViewModel model)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var application = new Application
    {
      Resources = { ["Headline"] = new Style(typeof(Label)) },
    };
    foreach (var key in UiMessageKey.All)
      application.Resources[key.Value] = UiLocalization.English.Localize(key);
    return new RewardsProgramStatusesPage(model);
  }

  private static View CreateRow(RewardsProgramStatusesPage page, RewardsProgramStatusRow row)
  {
    var layout = FindItemsLayout<RewardsProgramStatusRow>(page);
    var content = Assert.IsAssignableFrom<View>(BindableLayout.GetItemTemplate(layout)!.CreateContent());
    content.BindingContext = row;
    return content;
  }

  private static View CreateOptionRow(RewardsProgramStatusesPage page, RewardsProgramStatusOptionRow option)
  {
    var layout = FindItemsLayout<RewardsProgramStatusOptionRow>(page);
    var content = Assert.IsAssignableFrom<View>(BindableLayout.GetItemTemplate(layout)!.CreateContent());
    content.BindingContext = option;
    return content;
  }

  private static VerticalStackLayout FindItemsLayout<T>(RewardsProgramStatusesPage page) =>
      Descendants<VerticalStackLayout>(page).Single(candidate =>
          BindableLayout.GetItemsSource(candidate)?.Cast<object>().Any(item => item is T) == true);

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Descendants<T>(root).Single(value => value.AutomationId == automationId);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(10, TestContext.Current.CancellationToken);
    Assert.True(condition());
  }

  private static RewardsProgramStatus Status(string id) => new(
      id, "topic", new RewardsProgramSummary("topic", "Gold", "gold"), null, null);

  private sealed class Service : IRewardsProgramStatusesService
  {
    public Service(params RewardsProgramStatus[] values) : this(
        new RewardsProgramStatusPage(values, new PageInfo("next", true, null)))
    { Pages.Enqueue(new RewardsProgramStatusPage([Status("next")], new PageInfo(null, false, null))); }

    public Service(RewardsProgramStatusPage firstPage) => Pages.Enqueue(firstPage);

    public List<string> DeletedIds { get; } = [];
    public List<CreateRewardsProgramStatusBody> CreateBodies { get; } = [];
    public Queue<Exception> FetchErrors { get; } = [];
    public int Fetches { get; private set; }
    public Queue<RewardsProgramStatusPage> Pages { get; } = [];
    public int SearchCalls { get; private set; }
    public List<UpdateRewardsProgramStatusBody> UpdateBodies { get; } = [];
    public Task<RewardsProgramStatus>? CreatePending { get; set; }
    public Task<RewardsProgramStatus>? UpdatePending { get; set; }
    public Task<RewardsProgramStatusPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    {
      Fetches++;
      return FetchErrors.TryDequeue(out var error)
          ? Task.FromException<RewardsProgramStatusPage>(error)
          : Task.FromResult(Pages.Dequeue());
    }
    public Task<RewardsProgramStatus> CreateAsync(CreateRewardsProgramStatusBody body, CancellationToken cancellationToken = default)
    {
      CreateBodies.Add(body);
      return CreatePending ?? Task.FromResult(Status("created"));
    }
    public Task<RewardsProgramStatus> UpdateAsync(string id, UpdateRewardsProgramStatusBody body, CancellationToken cancellationToken = default)
    {
      UpdateBodies.Add(body);
      return UpdatePending ?? Task.FromResult(Status(id));
    }
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    { DeletedIds.Add(id); return Task.CompletedTask; }
    public Task<IReadOnlyList<RewardsProgramStatusOption>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
      SearchCalls++;
      return Task.FromResult<IReadOnlyList<RewardsProgramStatusOption>>(
          [new("topic", "Gold", "gold", "rewards_program_status")]);
    }
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  { public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance; }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }
}
