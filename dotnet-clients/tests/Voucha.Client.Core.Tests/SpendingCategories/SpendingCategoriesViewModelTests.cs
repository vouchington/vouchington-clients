using Voucha.Client.Core.Api;
using Voucha.Client.Core.SpendingCategories;
using Xunit;

namespace Voucha.Client.Core.Tests.SpendingCategories;

public sealed class SpendingCategoriesViewModelTests
{
  [Fact]
  public async Task PaginatesDeduplicatesAndRejectsStaleSearchResults()
  {
    var pendingSearch = new TaskCompletionSource<IReadOnlyList<SpendingCategoryOption>>();
    var service = new Service(Page(true, "next", Value("one"))) { SearchPending = pendingSearch.Task };
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.Pages.Enqueue(Page(false, null, Value("one", 2m), Value("two")));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["one", "two"], model.Rows.Select(row => row.Id));
    Assert.Equal("USD\u00A02.00", model.Rows[0].LocalizedAmount);

    model.SearchQuery = "old";
    var search = model.SearchAsync(TestContext.Current.CancellationToken);
    model.SearchQuery = "new";
    pendingSearch.SetResult([new("topic", "Groceries", "groceries")]);
    await search;
    Assert.Empty(model.SearchRows);
  }

  [Fact]
  public async Task NoOpAndReadOnlyRowsSuppressMutations()
  {
    var service = new Service(Page(false, null, Value("owned"), Value("member", canManage: false)));
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    model.BeginEdit(model.Rows[0]);
    await model.SaveAsync(TestContext.Current.CancellationToken);
    model.BeginEdit(model.Rows[1]);
    await model.DeleteAsync(model.Rows[1], TestContext.Current.CancellationToken);

    Assert.Equal(0, service.Updates);
    Assert.Equal(0, service.Deletes);
    Assert.Equal(["owned", "member"], model.Rows.Select(row => row.Id));
  }

  [Fact]
  public async Task OptimisticDeleteRollsBackOnFailure()
  {
    var deletion = new TaskCompletionSource();
    var service = new Service(Page(false, null, Value("one"), Value("two"))) { DeletePending = deletion.Task };
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    var delete = model.DeleteAsync(model.Rows[0], TestContext.Current.CancellationToken);
    Assert.Equal(["two"], model.Rows.Select(row => row.Id));
    deletion.SetException(new InvalidOperationException("offline"));
    await delete;
    Assert.Equal(["one", "two"], model.Rows.Select(row => row.Id));
  }

  private static SpendingCategory Value(string id, decimal amount = 1m, bool canManage = true) => new(
      id, $"{id}-topic", new Money(decimal.ToInt64(amount * 100), "usd"), "monthly", null,
      canManage ? "individual" : "household", canManage,
      new SpendingCategorySummary($"{id}-topic", id, id));
  private static SpendingCategoryPage Page(bool next, string? cursor, params SpendingCategory[] values) =>
      new(values, new PageInfo(cursor, next, null));

  private sealed class Service : ISpendingCategoriesService
  {
    public Service(SpendingCategoryPage page) => Pages.Enqueue(page);
    public Queue<SpendingCategoryPage> Pages { get; } = [];
    public Task<IReadOnlyList<SpendingCategoryOption>>? SearchPending { get; init; }
    public Task DeletePending { get; init; } = Task.CompletedTask;
    public int Updates { get; private set; }
    public int Deletes { get; private set; }
    public Task<SpendingCategoryPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) => Task.FromResult(Pages.Dequeue());
    public Task<SpendingCategory> CreateAsync(CreateSpendingCategoryBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<SpendingCategory> UpdateAsync(string id, UpdateSpendingCategoryBody body, CancellationToken cancellationToken = default) { Updates++; return Task.FromResult(Value(id)); }
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) { Deletes++; return DeletePending; }
    public Task<IReadOnlyList<SpendingCategoryOption>> SearchAsync(string query, CancellationToken cancellationToken = default) => SearchPending ?? Task.FromResult<IReadOnlyList<SpendingCategoryOption>>([]);
  }
}
