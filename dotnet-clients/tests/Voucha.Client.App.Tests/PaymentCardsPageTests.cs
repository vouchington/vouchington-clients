using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.PaymentCards;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class PaymentCardsPageTests
{
  [Fact]
  public async Task RendersControlsLabeledDetailsAndNoUuidText()
  {
    var id = "6c90acb5-e92f-4336-86e3-05e86cf11b48";
    var card = Card(id, "Travel card", "Keep this memo") with
    {
      CreditLimit = new Money(0, "usd"),
      OpenedOn = new DateOnly(2024, 1, 2),
      IsAuthorizedUser = true,
    };
    var viewModel = new PaymentCardsViewModel(new Service(Page(false, null, card)));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single());
    var page = CreatePage(viewModel);
    var row = CreateRow(page, viewModel.Rows.Single());

    Assert.NotNull(Find<Entry>(page, "payment-cards-search-input"));
    Assert.NotNull(Find<Picker>(page, "payment-cards-parent-picker"));
    Assert.Equal(3, Descendants<DatePicker>(page).Count());
    Assert.Equal("Credit limit: USD\u00A00.00", Find<Label>(row, "payment-card-limit").Text);
    Assert.Equal("Travel card", Find<Label>(row, "payment-card-name").Text);
    Assert.StartsWith("Opened: ", Find<Label>(row, "payment-card-opened").Text);
    Assert.Equal("Note: Keep this memo", Find<Label>(row, "payment-card-note").Text);
    Assert.Equal("Authorized user, no parent card", Find<Label>(row, "payment-card-parent").Text);
    var renderedText = Descendants<Label>(page).Concat(Descendants<Label>(row)).Select(label => label.Text);
    Assert.DoesNotContain(renderedText, text => text?.Contains(id, StringComparison.Ordinal) == true);
    Assert.Empty(Descendants<WebView>(page));
  }

  [Fact]
  public async Task RendersContinuationErrorAndRetryPreservesRows()
  {
    var service = new Service(
        Page(true, "next", Card("a")),
        new InvalidOperationException("offline"),
        Page(false, null, Card("b")));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    var page = CreatePage(viewModel);
    Assert.True(Find<VerticalStackLayout>(page, "payment-cards-error").IsVisible);

    Find<Button>(page, "payment-cards-retry").SendClicked();
    await WaitUntilAsync(() => service.Fetches.Count == 3 && !viewModel.HasError);

    Assert.Equal([null, "next", "next"], service.Fetches);
    Assert.Equal(["a", "b"], viewModel.Cards.Select(card => card.Id));
    Assert.False(Find<VerticalStackLayout>(page, "payment-cards-error").IsVisible);
  }

  [Fact]
  public async Task ReappearingSuppressesDuplicateLoadsAndRecreatedRoutesUseTransientViewModels()
  {
    var samePageService = new Service(Page(false, null, Card("same")));
    var samePage = CreatePage(new PaymentCardsViewModel(samePageService));
    await samePage.LoadForAppearanceAsync(TestContext.Current.CancellationToken);
    await samePage.LoadForAppearanceAsync(TestContext.Current.CancellationToken);
    Assert.Single(samePageService.Fetches);

    var routeService = new Service(
        Page(false, null, Card("first")),
        Page(false, null, Card("second")));
    var services = new ServiceCollection();
    MauiProgram.AddPaymentCardServices(services);
    var controller = new UiLocaleController(new Languages());
    services.AddSingleton<IUiLocaleController>(controller);
    services.AddSingleton<IUiLocalization>(new UiLocalization(controller));
    services.AddSingleton<IPaymentCardsService>(routeService);
    using var provider = services.BuildServiceProvider();
    var firstRoute = provider.GetRequiredService<PaymentCardsPage>();
    var recreatedRoute = provider.GetRequiredService<PaymentCardsPage>();
    Assert.NotSame(firstRoute.BindingContext, recreatedRoute.BindingContext);

    var routeRoot = new ContentPage();
    var navigation = new NavigationPage(routeRoot);
    await navigation.PushAsync(firstRoute);
    await firstRoute.LoadForAppearanceAsync(TestContext.Current.CancellationToken);
    Assert.Same(firstRoute, await navigation.PopAsync());
    Assert.Same(routeRoot, navigation.CurrentPage);
    await navigation.PushAsync(recreatedRoute);
    await recreatedRoute.LoadForAppearanceAsync(TestContext.Current.CancellationToken);
    Assert.Same(recreatedRoute, navigation.CurrentPage);
    Assert.Equal(2, routeService.Fetches.Count);
  }

  [Fact]
  public async Task ParentPickerTracksAuthorizedUserToggleAndDraftSelection()
  {
    var parent = Card("parent", "Parent card");
    var child = Card("child", "Child card") with
    {
      IsAuthorizedUser = true,
      AuthorizedUserOfId = parent.Id,
      AuthorizedUserOfCard = new PaymentCardParentSummary(parent.Id, null, null, parent.Card),
    };
    var viewModel = new PaymentCardsViewModel(new Service(Page(false, null, parent, child)));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single(row => row.Id == child.Id));
    var page = CreatePage(viewModel);
    var picker = Find<Picker>(page, "payment-cards-parent-picker");

    Assert.Equal(parent.Id, Assert.IsType<PaymentCardOption>(picker.SelectedItem).Id);
    viewModel.Draft!.IsAuthorizedUser = false;
    Assert.Null(Assert.IsType<PaymentCardOption>(picker.SelectedItem).Id);
    viewModel.Draft.IsAuthorizedUser = true;
    Assert.Null(Assert.IsType<PaymentCardOption>(picker.SelectedItem).Id);

    picker.SelectedItem = picker.ItemsSource.Cast<PaymentCardOption>().Single(option => option.Id == parent.Id);
    Assert.Equal(parent.Id, viewModel.Draft.AuthorizedUserOfId);
  }

  [Fact]
  public void PreviouslyMeasuredParentPaginationHiddenByAncestorStopsRequestsAndRearmsWhenReopened()
  {
    var content = new VerticalStackLayout();
    var draftEditor = new VerticalStackLayout();
    var control = new HybridPaginationControl { HasMore = true, HeightRequest = 44 };
    content.Children.Add(draftEditor);
    draftEditor.Children.Add(control);
    const double previouslyMeasuredHeight = 44;
    var visibility = new ViewportPaginationTrigger<HybridPaginationControl>();
    var requests = 0;
    control.LoadNextPageRequested += (_, _) => requests++;

    EvaluateParentPagination();
    Assert.Equal(1, requests);

    draftEditor.IsVisible = false;
    EvaluateParentPagination();
    Assert.Equal(1, requests);

    draftEditor.IsVisible = true;
    EvaluateParentPagination();
    Assert.Equal(2, requests);

    void EvaluateParentPagination()
    {
      IReadOnlyList<(HybridPaginationControl Control, double Top, double Height)> candidates =
          PaymentCardsPage.IsEffectivelyVisible(control, content)
              ? [(control, 0, previouslyMeasuredHeight)]
              : [];
      foreach (var entered in visibility.EnteredViewport(candidates, viewportTop: 0, viewportHeight: 100))
        entered.TryLoadAutomatically();
    }
  }

  private static PaymentCardsPage CreatePage(PaymentCardsViewModel viewModel)
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
    return new PaymentCardsPage(viewModel);
  }

  private static Element CreateRow(PaymentCardsPage page, PaymentCardRow row)
  {
    var host = Descendants<VerticalStackLayout>(page)
        .Single(layout => BindableLayout.GetItemsSource(layout)?.Cast<object>().Any(item => item is PaymentCardRow) == true);
    var template = Assert.IsType<DataTemplate>(BindableLayout.GetItemTemplate(host));
    var content = Assert.IsAssignableFrom<Element>(template.CreateContent());
    content.BindingContext = row;
    return content;
  }

  private static T Find<T>(Element root, string automationId) where T : Element =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == automationId);

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }

  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(10);
    Assert.True(condition());
  }

  private static PaymentCardPage Page(bool more, string? cursor, params PaymentCard[] cards) =>
      new(cards, new PageInfo(cursor, more, cards.FirstOrDefault()?.Id));

  private static PaymentCard Card(string id, string name = "Visible card", string? note = null) => new(
      id, $"topic-{id}", null, null, null, null, false, null, note,
      new PaymentCardTopic($"topic-{id}", name, "visible-card"), null);

  private sealed class Languages : IDeviceLanguageProvider
  { public IReadOnlyList<string> PreferredLanguages { get; } = ["en"]; }

  private sealed class Service(params object[] results) : IPaymentCardsService
  {
    private readonly Queue<object> queue = new(results);
    public List<string?> Fetches { get; } = [];
    public Task<PaymentCardPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    {
      Fetches.Add(after);
      var result = queue.Dequeue();
      return result is Exception error
          ? Task.FromException<PaymentCardPage>(error)
          : Task.FromResult((PaymentCardPage)result);
    }
    public Task<PaymentCard> CreateAsync(string topicId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PaymentCard> UpdateAsync(string id, UpdatePaymentCardBody body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<PaymentCardTopic>> SearchTopicsAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PaymentCardTopic>>([]);
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  { public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance; }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => new Timer();
  }

  private sealed class Timer : IDispatcherTimer
  {
    public TimeSpan Interval { get; set; }
    public bool IsRepeating { get; set; }
    public bool IsRunning { get; private set; }
    public event EventHandler? Tick;
    public void Start() { IsRunning = true; Tick?.Invoke(this, EventArgs.Empty); IsRunning = false; }
    public void Stop() => IsRunning = false;
  }
}
