using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.SpendingCategories;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class SpendingCategoriesPageTests
{
  [Fact]
  public async Task RendersLocalizedFrequencyAndExposesMemberReadOnlyState()
  {
    var model = new SpendingCategoriesViewModel(new Service(Page(false, null, Value("owned", 1.5m), Value("member", 2m, false))));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(model);
    var rows = Descendants<VerticalStackLayout>(page).Where(layout => BindableLayout.GetItemsSource(layout)?.Cast<object>().Any(item => item is SpendingCategoryRow) == true).Single();
    var template = BindableLayout.GetItemTemplate(rows)!;
    var owned = Assert.IsAssignableFrom<View>(template.CreateContent()); owned.BindingContext = model.Rows[0];
    var member = Assert.IsAssignableFrom<View>(template.CreateContent()); member.BindingContext = model.Rows[1];
    Assert.Contains("Monthly", Find<Label>(owned, "spending-category-amount-frequency").Text, StringComparison.Ordinal);
    Assert.Equal("usd", Find<Picker>(page, "spending-categories-create-currency").SelectedItem);
    Assert.False(model.Rows[1].CanManage);
    Assert.True(model.Rows[1].IsReadOnlyHousehold);
  }

  [Fact]
  public async Task ConfirmedCancellationRollsBackAndContinuationRequiresRetry()
  {
    using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    var service = new Service(Page(true, "next", Value("a"))) { DeleteResult = Task.FromCanceled(cancellation.Token) };
    var model = new SpendingCategoriesViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(model);
    await model.DeleteAsync(model.Rows.Single(), cancellation.Token);
    Assert.Single(model.Rows);
    service.FetchErrors.Enqueue(new InvalidOperationException("offline"));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasContinuationError);
    service.Pages.Enqueue(Page(false, null, Value("b")));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, service.Fetches);
    await model.RetryAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["a", "b"], model.Categories.Select(value => value.Id));
    page.Dispose();
  }

  private static SpendingCategoriesPage CreatePage(SpendingCategoriesViewModel model)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Metadata"] = new Style(typeof(Label)) } };
    return new SpendingCategoriesPage(model);
  }
  private static T Find<T>(Element root, string id) where T : Element => Descendants<T>(root).Single(value => value.AutomationId == id);
  private static IEnumerable<T> Descendants<T>(Element root) where T : Element => root.GetVisualTreeDescendants().OfType<T>();
  private static SpendingCategory Value(string id, decimal amount = 1m, bool canManage = true) => new(id, $"{id}-topic", new Money(decimal.ToInt64(amount * 100), "usd"), "monthly", null, canManage ? "individual" : "household", canManage, new SpendingCategorySummary($"{id}-topic", id, id));
  private static SpendingCategoryPage Page(bool next, string? cursor, params SpendingCategory[] values) => new(values, new PageInfo(cursor, next, null));
  private sealed class Service(SpendingCategoryPage page) : ISpendingCategoriesService
  {
    public Queue<SpendingCategoryPage> Pages { get; } = new([page]);
    public Queue<Exception> FetchErrors { get; } = [];
    public Task DeleteResult { get; init; } = Task.CompletedTask;
    public int Fetches { get; private set; }
    public Task<SpendingCategoryPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    { Fetches++; return FetchErrors.TryDequeue(out var error) ? Task.FromException<SpendingCategoryPage>(error) : Task.FromResult(Pages.Dequeue()); }
    public Task<SpendingCategory> CreateAsync(CreateSpendingCategoryBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<SpendingCategory> UpdateAsync(string id, UpdateSpendingCategoryBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => DeleteResult;
    public Task<IReadOnlyList<SpendingCategoryOption>> SearchAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SpendingCategoryOption>>([]);
  }
  private sealed class ImmediateDispatcherProvider : IDispatcherProvider { public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance; }
  private sealed class ImmediateDispatcher : IDispatcher
  { public static ImmediateDispatcher Instance { get; } = new(); public bool IsDispatchRequired => false; public bool Dispatch(Action action) { action(); return true; } public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; } public IDispatcherTimer CreateTimer() => throw new NotSupportedException(); }
}
