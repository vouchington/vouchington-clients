using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.SpendingCategories;
using Xunit;

namespace Voucha.Client.Core.Tests.SpendingCategories;

public sealed class SpendingCategoriesViewModelReviewTests
{
  [Fact]
  public async Task CallerCancellationIsSilentAcrossReadsSearchAndMutations()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var service = new ScriptedService { Fetch = _ => Cancelled<SpendingCategoryPage>(), Search = _ => Cancelled<IReadOnlyList<SpendingCategoryOption>>() };
    var loading = new SpendingCategoriesViewModel(service);
    await loading.LoadAsync(cancellation.Token);
    Assert.False(loading.HasError);

    service.Fetch = after => after is null ? Task.FromResult(Page(true, "next", Value("one"))) : Cancelled<SpendingCategoryPage>();
    var continuation = new SpendingCategoriesViewModel(service);
    await continuation.LoadAsync(TestContext.Current.CancellationToken);
    await continuation.LoadMoreAsync(cancellation.Token);
    Assert.False(continuation.HasError); Assert.False(continuation.HasContinuationError);

    var searching = new SpendingCategoriesViewModel(service) { SearchQuery = "food" };
    await searching.SearchAsync(cancellation.Token);
    Assert.False(searching.HasSearchError);

    var mutations = new SpendingCategoriesViewModel(service);
    service.Fetch = _ => Task.FromResult(Page(false, null, Value("one")));
    await mutations.LoadAsync(TestContext.Current.CancellationToken);
    mutations.CreateDraft.Amount = "2";
    service.Create = _ => Cancelled<SpendingCategory>();
    await mutations.CreateAsync(OptionRow(), cancellation.Token);
    Assert.False(mutations.HasError);

    mutations.BeginEdit(mutations.Rows.Single()); mutations.EditDraft!.Amount = "2";
    service.Update = _ => Cancelled<SpendingCategory>();
    await mutations.SaveAsync(cancellation.Token);
    Assert.False(mutations.HasError);

    service.Delete = _ => Cancelled();
    await mutations.DeleteAsync(mutations.Rows.Single(), cancellation.Token);
    Assert.False(mutations.HasError); Assert.Single(mutations.Rows);
  }

  [Fact]
  public async Task TransportCancellationSurfacesThroughExistingErrorState()
  {
    var service = new ScriptedService { Fetch = _ => Cancelled<SpendingCategoryPage>() };
    var loading = new SpendingCategoriesViewModel(service);
    await loading.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(loading.HasError);

    service.Fetch = after => after is null ? Task.FromResult(Page(true, "next", Value("one"))) : Cancelled<SpendingCategoryPage>();
    var continuation = new SpendingCategoriesViewModel(service);
    await continuation.LoadAsync(TestContext.Current.CancellationToken);
    await continuation.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.True(continuation.HasError); Assert.True(continuation.HasContinuationError);

    service.Search = _ => Cancelled<IReadOnlyList<SpendingCategoryOption>>();
    var searching = new SpendingCategoriesViewModel(service) { SearchQuery = "food" };
    await searching.SearchAsync(TestContext.Current.CancellationToken);
    Assert.True(searching.HasSearchError);

    service.Fetch = _ => Task.FromResult(Page(false, null, Value("one")));
    var creating = new SpendingCategoriesViewModel(service);
    await creating.LoadAsync(TestContext.Current.CancellationToken);
    creating.CreateDraft.Amount = "2"; service.Create = _ => Cancelled<SpendingCategory>();
    await creating.CreateAsync(OptionRow(), TestContext.Current.CancellationToken);
    Assert.True(creating.HasError);

    var saving = new SpendingCategoriesViewModel(service);
    await saving.LoadAsync(TestContext.Current.CancellationToken);
    saving.BeginEdit(saving.Rows.Single()); saving.EditDraft!.Amount = "2"; service.Update = _ => Cancelled<SpendingCategory>();
    await saving.SaveAsync(TestContext.Current.CancellationToken);
    Assert.True(saving.HasError);

    var deleting = new SpendingCategoriesViewModel(service);
    await deleting.LoadAsync(TestContext.Current.CancellationToken);
    service.Delete = _ => Cancelled();
    await deleting.DeleteAsync(deleting.Rows.Single(), TestContext.Current.CancellationToken);
    Assert.True(deleting.HasError); Assert.Single(deleting.Rows);
  }

  [Fact]
  public async Task PresentationRowsAreCachedAndInvalidatedWithTheirInputs()
  {
    var service = new ScriptedService
    {
      Fetch = _ => Task.FromResult(Page(false, null, Value("one"))),
      Search = _ => Task.FromResult<IReadOnlyList<SpendingCategoryOption>>([new("topic", "Food", "food")]),
      Create = _ => Task.FromResult(Value("created", amount: 2m)),
    };
    var model = new SpendingCategoriesViewModel(service);
    var changes = new List<string?>();
    model.PropertyChanged += (_, eventArgs) => changes.Add(eventArgs.PropertyName);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var initialRows = model.Rows;
    Assert.Same(initialRows, model.Rows);

    model.SearchQuery = "food";
    await model.SearchAsync(TestContext.Current.CancellationToken);
    var initialSearchRows = model.SearchRows;
    Assert.Same(initialSearchRows, model.SearchRows);

    model.CreateDraft.Amount = "2";
    await model.CreateAsync(OptionRow(), TestContext.Current.CancellationToken);
    Assert.NotSame(initialRows, model.Rows); Assert.Contains(nameof(model.Rows), changes);
    Assert.NotSame(initialSearchRows, model.SearchRows); Assert.Empty(model.SearchRows);

    model.SearchQuery = "food";
    await model.SearchAsync(TestContext.Current.CancellationToken);
    var rowsBeforeLocaleChange = model.Rows;
    var searchRowsBeforeLocaleChange = model.SearchRows;
    model.OnUiLocaleChanged();
    Assert.NotSame(rowsBeforeLocaleChange, model.Rows); Assert.NotSame(searchRowsBeforeLocaleChange, model.SearchRows);
    Assert.Contains(nameof(model.SearchRows), changes);
  }

  private static Task Cancelled() => Task.FromException(new OperationCanceledException());
  private static Task<T> Cancelled<T>() => Task.FromException<T>(new OperationCanceledException());
  private static SpendingCategoryOptionRow OptionRow() => new(new("topic", "Food", "food"), UiLocalization.English);
  private static SpendingCategory Value(string id, decimal amount = 1m) => new(
      id, $"{id}-topic", new Money(decimal.ToInt64(amount * 100), "usd"), "monthly", null, "individual", true,
      new SpendingCategorySummary($"{id}-topic", id, id));
  private static SpendingCategoryPage Page(bool next, string? cursor, params SpendingCategory[] values) =>
      new(values, new PageInfo(cursor, next, null));

  private sealed class ScriptedService : ISpendingCategoriesService
  {
    public Func<string?, Task<SpendingCategoryPage>> Fetch { get; set; } = _ => Task.FromResult(Page(false, null));
    public Func<CreateSpendingCategoryBody, Task<SpendingCategory>> Create { get; set; } = _ => Task.FromException<SpendingCategory>(new NotSupportedException());
    public Func<UpdateSpendingCategoryBody, Task<SpendingCategory>> Update { get; set; } = _ => Task.FromException<SpendingCategory>(new NotSupportedException());
    public Func<string, Task> Delete { get; set; } = _ => Task.CompletedTask;
    public Func<string, Task<IReadOnlyList<SpendingCategoryOption>>> Search { get; set; } = _ => Task.FromResult<IReadOnlyList<SpendingCategoryOption>>([]);

    public Task<SpendingCategoryPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) => Fetch(after);
    public Task<SpendingCategory> CreateAsync(CreateSpendingCategoryBody body, CancellationToken cancellationToken = default) => Create(body);
    public Task<SpendingCategory> UpdateAsync(string id, UpdateSpendingCategoryBody body, CancellationToken cancellationToken = default) => Update(body);
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Delete(id);
    public Task<IReadOnlyList<SpendingCategoryOption>> SearchAsync(string query, CancellationToken cancellationToken = default) => Search(query);
  }
}
