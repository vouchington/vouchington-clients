using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.SpendingCategories;
using Xunit;

namespace Voucha.Client.Core.Tests.SpendingCategories;

public sealed class SpendingCategoriesReconciliationTests
{
  [Fact]
  public void FromWireRejectsMissingNestedCategory()
  {
    var wire = new SpendingCategoryWire(
        "entry", "topic", new Money(100, "usd"), "monthly", null, "individual", true, null!);

    var error = Assert.Throws<JsonException>(() => SpendingCategory.FromWire(wire));

    Assert.Contains("spending_category", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task CreateAndUpdateOverlaysSurviveStaleReadsThenClearWhenObserved()
  {
    var service = new Service(Page(Value("one", 1m)));
    service.Creates.Enqueue(Value("created", 3m));
    service.Updates.Enqueue(Value("one", 2m));
    service.Reads.Enqueue(Page(Value("one", 1m)));
    service.Reads.Enqueue(Page(Value("one", 2m), Value("created", 3m)));
    service.Reads.Enqueue(Page(Value("one", 1m)));
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.CreateDraft.Amount = "3";

    await model.CreateAsync(new(new("topic", "Topic", "topic"), UiLocalization.English), TestContext.Current.CancellationToken);
    model.BeginEdit(model.Rows.Single(row => row.Id == "one"));
    model.EditDraft!.Amount = "2";
    await model.SaveAsync(TestContext.Current.CancellationToken);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal([("one", 200L), ("created", 300L)], model.Categories.Select(value => (value.Id, value.Amount.Amount)));

    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal([("one", 100L)], model.Categories.Select(value => (value.Id, value.Amount.Amount)));
  }

  [Fact]
  public async Task LoadMoreDoesNotTreatLocallyMergedValuesAsUpsertObservation()
  {
    var service = new Service(Page(true, "next", Value("one", 1m)));
    service.Updates.Enqueue(Value("one", 2m));
    service.Reads.Enqueue(Page(false, null, Value("two")));
    service.Reads.Enqueue(Page(false, null, Value("one", 1m)));
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.BeginEdit(model.Rows.Single());
    model.EditDraft!.Amount = "2";
    await model.SaveAsync(TestContext.Current.CancellationToken);

    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal([("one", 200L)], model.Categories.Select(value => (value.Id, value.Amount.Amount)));
  }

  [Fact]
  public async Task UpsertOverlayBridgesOneCompleteStaleTraversalThenRetiresForNewerServerValue()
  {
    var service = new Service(Page(Value("one", 1m)));
    service.Updates.Enqueue(Value("one", 2m));
    service.Reads.Enqueue(Page(Value("one", 1m)));
    service.Reads.Enqueue(Page(Value("one", 3m)));
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.BeginEdit(model.Rows.Single());
    model.EditDraft!.Amount = "2";
    await model.SaveAsync(TestContext.Current.CancellationToken);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new Money(200, "usd"), model.Categories.Single().Amount);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(new Money(300, "usd"), model.Categories.Single().Amount);
  }

  [Fact]
  public async Task TerminalPageRetirementUsesNewerUpsertValueObservedOnAnEarlierPage()
  {
    var service = new Service(Page(Value("one", 1m)));
    service.Updates.Enqueue(Value("one", 2m));
    service.Reads.Enqueue(Page(Value("one", 1m)));
    service.Reads.Enqueue(Page(true, "next", Value("one", 3m)));
    service.Reads.Enqueue(Page(false, null, Value("two")));
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.BeginEdit(model.Rows.Single());
    model.EditDraft!.Amount = "2";
    await model.SaveAsync(TestContext.Current.CancellationToken);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal([("one", 300L), ("two", 100L)], model.Categories.Select(value => (value.Id, value.Amount.Amount)));
  }

  [Fact]
  public async Task CompletedDeleteNeedsCompletePostMutationPaginationBeforeItClears()
  {
    var service = new Service(Page(Value("one"), Value("two")));
    service.Reads.Enqueue(Page(true, "next", Value("two")));
    service.Reads.Enqueue(Page(false, null, Value("one")));
    service.Reads.Enqueue(Page(true, "next", Value("two")));
    service.Reads.Enqueue(Page(false, null, Value("three")));
    service.Reads.Enqueue(Page(false, null, Value("one"), Value("two")));
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    await model.DeleteAsync(model.Rows.Single(row => row.Id == "one"), TestContext.Current.CancellationToken);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["two"], model.Categories.Select(value => value.Id));

    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["one", "two"], model.Categories.Select(value => value.Id));
  }

  private static SpendingCategory Value(string id, decimal amount = 1m) => new(
      id, $"{id}-topic", new Money(decimal.ToInt64(amount * 100), "usd"), "monthly", null, "individual", true,
      new SpendingCategorySummary($"{id}-topic", id, id));
  private static SpendingCategoryPage Page(params SpendingCategory[] values) => Page(false, null, values);
  private static SpendingCategoryPage Page(bool hasNextPage, string? endCursor, params SpendingCategory[] values) =>
      new(values, new PageInfo(endCursor, hasNextPage, null));

  private sealed class Service(SpendingCategoryPage initial) : ISpendingCategoriesService
  {
    public Queue<SpendingCategoryPage> Reads { get; } = new([initial]);
    public Queue<SpendingCategory> Creates { get; } = [];
    public Queue<SpendingCategory> Updates { get; } = [];
    public Task<SpendingCategoryPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) => Task.FromResult(Reads.Dequeue());
    public Task<SpendingCategory> CreateAsync(CreateSpendingCategoryBody body, CancellationToken cancellationToken = default) => Task.FromResult(Creates.Dequeue());
    public Task<SpendingCategory> UpdateAsync(string id, UpdateSpendingCategoryBody body, CancellationToken cancellationToken = default) => Task.FromResult(Updates.Dequeue());
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<SpendingCategoryOption>> SearchAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SpendingCategoryOption>>([]);
  }
}
