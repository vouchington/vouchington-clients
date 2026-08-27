using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.SpendingCategories;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.SpendingCategories;

public sealed class SpendingCategoriesCoverageTests
{
  [Fact]
  public async Task ApiServiceMapsPagesMutationsAndSearchResults()
  {
    var handler = new RecordingHandler(
    [
      new(WrappedList("one", pageInfo: null)),
      new(WrappedCategory("created", "Created")),
      new(WrappedCategory("updated", "Updated")),
      new("{}"),
      new("""{"results":[{"id":"topic","name":"Groceries","slug":"groceries"},{"id":null,"name":"discard","slug":"discard"}]}"""),
    ]);
    var service = new ApiSpendingCategoriesService(Client(handler));

    var page = await service.FetchAsync("after value", 2, TestContext.Current.CancellationToken);
    var created = await service.CreateAsync(new("topic", new Money(150, "usd"), "monthly", Note: "note"), TestContext.Current.CancellationToken);
    var updated = await service.UpdateAsync("created", new(Amount: new Money(200, "usd")), TestContext.Current.CancellationToken);
    await service.DeleteAsync("created", TestContext.Current.CancellationToken);
    var topics = await service.SearchAsync("groceries", TestContext.Current.CancellationToken);

    Assert.False(page.PageInfo.HasNextPage);
    Assert.Equal("one", page.Results.Single().Id);
    Assert.Equal("created", created.Id);
    Assert.Equal("updated", updated.Id);
    Assert.Equal([new SpendingCategoryOption("topic", "Groceries", "groceries")], topics);
    Assert.Equal("{\"spending_category_id\":\"topic\",\"amount\":{\"amount\":150,\"currency\":\"usd\"},\"spending_frequency\":\"monthly\",\"note\":\"note\"}", handler.Requests[1].Body);
    Assert.Equal(
    [
      (HttpMethod.Get, "/api/v1/my/spending-categories?after=after%20value&limit=2"),
      (HttpMethod.Post, "/api/v1/my/spending-categories"),
      (HttpMethod.Patch, "/api/v1/my/spending-categories/created"),
      (HttpMethod.Delete, "/api/v1/my/spending-categories/created"),
      (HttpMethod.Get, "/api/v1/topics?limit=10&q=groceries&spending_category=true"),
    ], handler.Requests.Select(request => (request.Method!, request.PathAndQuery!)));
  }

  [Fact]
  public async Task LoadingFailureAndContinuationRetryPreserveRows()
  {
    var service = new Service();
    service.Fetches.Enqueue(() => Task.FromException<SpendingCategoryPage>(new InvalidOperationException("offline")));
    service.Fetches.Enqueue(() => Task.FromResult(Page(true, "next", Value("one"))));
    service.Fetches.Enqueue(() => Task.FromException<SpendingCategoryPage>(new InvalidOperationException()));
    service.Fetches.Enqueue(() => Task.FromResult(Page(false, null, Value("two"))));
    var model = new SpendingCategoriesViewModel(service);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasError); Assert.Equal("offline", model.Error);
    await model.RetryAsync(TestContext.Current.CancellationToken);
    Assert.False(model.HasError); Assert.True(model.HasNextPage);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasContinuationError); Assert.Equal(["one"], model.Categories.Select(value => value.Id));
    await model.RetryAsync(TestContext.Current.CancellationToken);

    Assert.False(model.HasContinuationError);
    Assert.Equal(["one", "two"], model.Categories.Select(value => value.Id));
  }

  [Fact]
  public async Task CreateEditAndDeleteMutationsUpdatePresentationState()
  {
    var service = new Service();
    service.Fetches.Enqueue(() => Task.FromResult(Page(false, null, Value("one", note: "before"))));
    service.Creates.Enqueue(() => Task.FromResult(Value("created", amount: 3m)));
    service.Updates.Enqueue(() => Task.FromResult(Value("one", amount: 2m, note: "after")));
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    await model.CreateAsync(new(new("topic", "Topic", "topic"), UiLocalization.English), TestContext.Current.CancellationToken);
    Assert.True(model.HasError);
    model.CreateDraft.Amount = "3"; model.CreateDraft.Note = "created note"; model.SearchQuery = "clear me";
    await model.CreateAsync(new(new("topic", "Topic", "topic"), UiLocalization.English), TestContext.Current.CancellationToken);
    Assert.Equal("created", model.Categories.Last().Id); Assert.Equal(string.Empty, model.SearchQuery);

    model.BeginEdit(model.Rows.Single(row => row.Id == "one"));
    Assert.True(model.HasEditDraft);
    model.EditDraft!.Amount = "2"; model.EditDraft.Note = "after";
    await model.SaveAsync(TestContext.Current.CancellationToken);
    Assert.False(model.HasEditDraft);
    Assert.Equal(new Money(200, "usd"), model.Categories.Single(value => value.Id == "one").Amount);

    await model.DeleteAsync(model.Rows.Single(row => row.Id == "created"), TestContext.Current.CancellationToken);
    Assert.DoesNotContain(model.Categories, value => value.Id == "created");
    Assert.Equal(1, service.Deletes);
  }

  [Fact]
  public async Task CreateIgnoresDuplicateSubmissionWhileTheFirstRequestIsPending()
  {
    var created = new TaskCompletionSource<SpendingCategory>();
    var service = new Service();
    service.Fetches.Enqueue(() => Task.FromResult(Page(false, null, Value("one"))));
    service.Creates.Enqueue(() => created.Task);
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.CreateDraft.Amount = "3";
    var row = new SpendingCategoryOptionRow(new("topic", "Topic", "topic"), UiLocalization.English);

    var first = model.CreateAsync(row, TestContext.Current.CancellationToken);
    var duplicate = model.CreateAsync(row, TestContext.Current.CancellationToken);
    Assert.True(model.IsCreating); Assert.Equal(1, service.CreateCalls);
    created.SetResult(Value("created", amount: 3m));
    await Task.WhenAll(first, duplicate);

    Assert.False(model.IsCreating); Assert.Equal(1, service.CreateCalls);
    Assert.Contains(model.Categories, value => value.Id == "created");
  }

  [Fact]
  public async Task MutationAndSearchFailuresRemainVisible()
  {
    var service = new Service();
    service.Fetches.Enqueue(() => Task.FromResult(Page(false, null, Value("one"))));
    service.Creates.Enqueue(() => Task.FromException<SpendingCategory>(new InvalidOperationException()));
    service.Searches.Enqueue(() => Task.FromException<IReadOnlyList<SpendingCategoryOption>>(new InvalidOperationException("search unavailable")));
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);

    model.CreateDraft.Amount = "1";
    await model.CreateAsync(new(new("topic", "Topic", "topic"), UiLocalization.English), TestContext.Current.CancellationToken);
    Assert.True(model.HasError); Assert.DoesNotContain(model.Categories, value => value.Id == "created");
    model.SearchQuery = "topic";
    await model.SearchAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasSearchError); Assert.Equal("search unavailable", model.LocalizedSearchErrorMessage);
    model.OnUiLocaleChanged();
  }

  [Fact]
  public async Task CancelledDeleteRestoresRowWithoutErrorAndClearsMutationState()
  {
    using var cancellation = new CancellationTokenSource();
    var deletion = new TaskCompletionSource();
    var service = new Service();
    service.Fetches.Enqueue(() => Task.FromResult(Page(false, null, Value("one"))));
    service.DeletesQueue.Enqueue(() => deletion.Task);
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var row = model.Rows.Single();

    var delete = model.DeleteAsync(row, cancellation.Token);
    Assert.Equal(1, service.Deletes); Assert.True(model.IsMutating(row.Id));
    Assert.DoesNotContain(model.Categories, value => value.Id == row.Id);
    cancellation.Cancel(); deletion.SetCanceled(cancellation.Token);
    await delete;

    Assert.False(model.HasError); Assert.False(model.IsMutating(row.Id));
    Assert.Equal(["one"], model.Categories.Select(value => value.Id));
  }

  [Fact]
  public void DraftAndRowsHandleLocalizedValuesCapabilitiesAndUserContent()
  {
    var original = Value("entry", amount: 1.25m, note: "A note") with { OwnerType = "household", CanManage = false };
    var draft = new SpendingCategoryDraft(original, System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
    Assert.Equal("1,25", draft.Amount);
    Assert.True(draft.TryBuildCreate("topic", out _));
    draft.Amount = "2,5"; draft.Frequency = "monthly"; draft.Note = string.Empty;
    Assert.True(draft.TryBuildUpdate(out var update));
    Assert.Equal(new Money(250, "usd"), update!.Amount); Assert.Equal(JsonNullableString.Null, update.Note);
    Assert.True(draft.TryBuildCreate("topic", out _));
    draft.Frequency = "annually";
    Assert.True(draft.TryBuildCreate("topic", out var create)); Assert.Equal("annually", create!.SpendingFrequency);

    var row = new SpendingCategoryRow(original, UiLocalization.English);
    Assert.Equal("entry", row.Id);
    Assert.Equal("USD\u00A01.25 Monthly", row.LocalizedAmountAndFrequency);
    var jpyRow = row with { Value = original with { Amount = new Money(2, "jpy") } };
    Assert.Equal("JPY\u00A02 Monthly", jpyRow.LocalizedAmountAndFrequency);
    Assert.Equal("A note", row.LocalizedNote); Assert.False(row.CanManage); Assert.True(row.IsReadOnlyHousehold);
    Assert.Equal("entry", new SpendingCategoryOptionRow(new("topic", "entry", "entry"), UiLocalization.English).LocalizedName);
  }

  private static VouchaApiClient Client(HttpMessageHandler handler) =>
      new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

  private static SpendingCategory Value(string id, decimal amount = 1m, string? note = null) => new(
      id, $"{id}-topic", new Money(decimal.ToInt64(amount * 100), "usd"), "monthly", note, "individual", true,
      new SpendingCategorySummary($"{id}-topic", id, id));
  private static SpendingCategoryPage Page(bool next, string? cursor, params SpendingCategory[] values) =>
      new(values, new PageInfo(cursor, next, null));
  private static string WrappedList(string id, string? pageInfo) =>
      "{\"results\":[" + Wire(id, id) + "],\"page_info\":" + (pageInfo ?? "null") + "}";
  private static string WrappedCategory(string id, string name) => "{\"spending_category\":" + Wire(id, name) + "}";
  private static string Wire(string id, string name) =>
      "{\"id\":\"" + id + "\",\"spending_category_id\":\"topic\",\"amount\":{\"amount\":150,\"currency\":\"usd\"},\"spending_frequency\":\"monthly\",\"note\":null,\"owner_type\":\"individual\",\"can_manage\":true,\"spending_category\":{\"id\":\"topic\",\"name\":\"" + name + "\",\"slug\":\"" + name.ToLowerInvariant() + "\"}}";

  private sealed class Service : ISpendingCategoriesService
  {
    public Queue<Func<Task<SpendingCategoryPage>>> Fetches { get; } = [];
    public Queue<Func<Task<SpendingCategory>>> Creates { get; } = [];
    public Queue<Func<Task<SpendingCategory>>> Updates { get; } = [];
    public Queue<Func<Task>> DeletesQueue { get; } = [];
    public Queue<Func<Task<IReadOnlyList<SpendingCategoryOption>>>> Searches { get; } = [];
    public int CreateCalls { get; private set; }
    public int Deletes { get; private set; }
    public Task<SpendingCategoryPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default) => Fetches.Dequeue()();
    public Task<SpendingCategory> CreateAsync(CreateSpendingCategoryBody body, CancellationToken cancellationToken = default)
    {
      CreateCalls++;
      return Creates.Dequeue()();
    }
    public Task<SpendingCategory> UpdateAsync(string id, UpdateSpendingCategoryBody body, CancellationToken cancellationToken = default) => Updates.Dequeue()();
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
      Deletes++;
      return DeletesQueue.Count == 0 ? Task.CompletedTask : DeletesQueue.Dequeue()();
    }
    public Task<IReadOnlyList<SpendingCategoryOption>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        Searches.Count == 0 ? Task.FromResult<IReadOnlyList<SpendingCategoryOption>>([]) : Searches.Dequeue()();
  }
}
