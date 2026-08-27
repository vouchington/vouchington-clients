using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.PointValuations;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class PointValuationsPageTests
{
  [Fact]
  public async Task RendersLocalizedValueAndNeverShowsUuid()
  {
    const string id = "6c90acb5-e92f-4336-86e3-05e86cf11b48";
    var value = new PointValuation(id, "program",
        new ScaledMoney(1_500_000, "usd", ScaledMoney.RequiredScale), "Useful note",
        new RewardsProgramSummary("program", "Travel Rewards", "travel-rewards"));
    var model = new PointValuationsViewModel(new Service(value));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(model);
    var row = CreateRow(page, model.Rows.Single());
    Assert.Equal("Travel Rewards", Find<Label>(row, "point-valuation-name").Text);
    Assert.Contains("1.5", Find<Label>(row, "point-valuation-value").Text, StringComparison.Ordinal);
    Assert.Equal("Useful note", Find<Label>(row, "point-valuation-note").Text);
    Assert.DoesNotContain(Descendants<Label>(page).Concat(Descendants<Label>(row)),
        label => label.Text?.Contains(id, StringComparison.Ordinal) == true);
    Assert.Empty(Descendants<WebView>(page));
  }

  [Fact]
  public void RegistrationCreatesTransientPagesAndViewModels()
  {
    EnsureResources();
    var services = new ServiceCollection();
    MauiProgram.AddPointValuationServices(services);
    services.AddSingleton<IPointValuationsService>(new Service());
    var controller = new UiLocaleController(new Languages());
    services.AddSingleton<IUiLocaleController>(controller);
    services.AddSingleton<IUiLocalization>(new UiLocalization(controller));
    using var provider = services.BuildServiceProvider();
    Assert.NotSame(provider.GetRequiredService<PointValuationsPage>(), provider.GetRequiredService<PointValuationsPage>());
  }

  [Fact]
  public void DisposalStopsLocaleDrivenViewModelUpdates()
  {
    var controller = new UiLocaleController(new Languages());
    var model = new PointValuationsViewModel(
        new Service(), new UiLocalization(controller), controller);
    var page = CreatePage(model);
    var notifications = 0;
    model.PropertyChanged += (_, _) => notifications++;

    page.Dispose();
    controller.ApplySavedLocale("fr");

    Assert.Equal(0, notifications);
  }

  [Fact]
  public async Task DeleteHandlerRollsBackRequestedCancellationWithoutEscaping()
  {
    const string id = "6c90acb5-e92f-4336-86e3-05e86cf11b48";
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var service = new Service(new PointValuation(id, "program",
        new ScaledMoney(1_000_000, "usd", ScaledMoney.RequiredScale), null,
        new RewardsProgramSummary("program", "Travel Rewards", "travel-rewards")))
    { DeleteResult = Task.FromCanceled(cancellation.Token) };
    var model = new PointValuationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(model);
    await page.DeleteConfirmedAsync(model.Rows.Single(), cancellation.Token);
    Assert.Single(model.Rows);
    Assert.False(model.HasError);
  }

  [Fact]
  public async Task ScrollStopsAfterContinuationFailureUntilExplicitRetry()
  {
    var service = new Service(new PointValuationPage(
        [Value("a")], new PageInfo("next", true, null)));
    var model = new PointValuationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(model);
    service.FetchErrors.Enqueue(new InvalidOperationException("offline"));

    await page.LoadMoreForScrollAsync(500, 500, 900, TestContext.Current.CancellationToken);
    Assert.True(model.HasContinuationError);
    Assert.Equal(2, service.Fetches);

    service.Pages.Enqueue(new PointValuationPage([Value("b")], new PageInfo(null, false, null)));
    await page.LoadMoreForScrollAsync(500, 500, 900, TestContext.Current.CancellationToken);
    Assert.Equal(2, service.Fetches);

    await page.RetryAsync(TestContext.Current.CancellationToken);
    Assert.Equal(3, service.Fetches);
    Assert.Equal(["a", "b"], model.Valuations.Select(value => value.Id));
  }

  private static PointValuationsPage CreatePage(PointValuationsViewModel model)
  {
    EnsureResources();
    return new PointValuationsPage(model);
  }

  private static void EnsureResources()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["Headline"] = new Style(typeof(Label)),
        ["Metadata"] = new Style(typeof(Label)),
      },
    };
  }

  private static View CreateRow(PointValuationsPage page, PointValuationRow row)
  {
    var layout = Descendants<VerticalStackLayout>(page).Single(candidate =>
        BindableLayout.GetItemsSource(candidate)?.Cast<object>()
            .Any(item => item is PointValuationRow) == true);
    var template = BindableLayout.GetItemTemplate(layout)!;
    var content = Assert.IsAssignableFrom<View>(template.CreateContent());
    content.BindingContext = row;
    return content;
  }

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Descendants<T>(root).Single(value => value.AutomationId == automationId);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private static PointValuation Value(string id) => new(
      id, "program", new ScaledMoney(1_000_000, "usd", ScaledMoney.RequiredScale), null,
      new RewardsProgramSummary("program", "Travel Rewards", "travel-rewards"));

  private sealed class Languages : IDeviceLanguageProvider
  { public IReadOnlyList<string> PreferredLanguages { get; } = ["en"]; }

  private sealed class Service : IPointValuationsService
  {
    public Service(params PointValuation[] values) : this(
        new PointValuationPage(values, new PageInfo(null, false, null)))
    { }
    public Service(PointValuationPage firstPage) => Pages.Enqueue(firstPage);

    public Task DeleteResult { get; init; } = Task.CompletedTask;
    public Queue<PointValuationPage> Pages { get; } = [];
    public Queue<Exception> FetchErrors { get; } = [];
    public int Fetches { get; private set; }
    public Task<PointValuationPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    {
      Fetches++;
      return FetchErrors.TryDequeue(out var error)
          ? Task.FromException<PointValuationPage>(error)
          : Task.FromResult(Pages.Dequeue());
    }
    public Task<PointValuation> CreateAsync(CreatePointValuationBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PointValuation> UpdateAsync(string id, UpdatePointValuationBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => DeleteResult;
    public Task<IReadOnlyList<RewardsProgramOption>> SearchAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RewardsProgramOption>>([]);
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
