using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.MembershipAdministration;

namespace Voucha.Client.App.Pages;

public sealed class MembershipGrantPage : ContentPage, IUiLocaleChangeListener, IDisposable
{
  private readonly MembershipGrantViewModel viewModel;
  private readonly IUiLocalization localization;
  private readonly Entry query = UiCopy.Bind(new Entry { AutomationId = "membership-grant-query" }, Entry.PlaceholderProperty, UiMessageKey.NativeSwiftMembershipSearchUsers);
  private readonly Picker plan = new() { AutomationId = "membership-grant-plan" };
  private readonly Picker sku = new() { AutomationId = "membership-grant-sku" };
  private readonly Entry durationDays = new() { AutomationId = "membership-grant-duration-days", Keyboard = Keyboard.Numeric };
  private readonly Label errorMessage = new() { TextColor = Colors.IndianRed };
  private readonly Label successMessage = new() { TextColor = Colors.ForestGreen };
  private readonly Label planStatus = new();
  private readonly Label noSkus = new();
  private readonly Label selectedUser = new();
  private readonly Button searchButton;
  private readonly Button submitButton;
  private readonly Button retryButton;
  private readonly CollectionView results = new();
  private readonly IDisposable localeSubscription;
  private bool disposed;
  private bool isSynchronizingSelection;

  public MembershipGrantPage(
      MembershipGrantViewModel viewModel,
      IUiLocalization localization,
      IUiLocaleController localeController)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
    localeSubscription = (localeController ?? throw new ArgumentNullException(nameof(localeController)))
        .SubscribeLocaleChanges(this);
    BindingContext = viewModel;
    viewModel.PropertyChanged += OnChanged;
    query.SetBinding(Entry.TextProperty, nameof(MembershipGrantViewModel.Query), BindingMode.TwoWay);
    plan.SelectedIndexChanged += OnPlanSelected;
    sku.SelectedIndexChanged += OnSkuSelected;
    durationDays.SetBinding(Entry.TextProperty, nameof(MembershipGrantViewModel.DurationDays), BindingMode.TwoWay);
    Title = localization.Localize(UiMessageKey.NativeSwiftMembershipMembershipGrants);
    searchButton = LocalizedButton(UiMessageKey.NativeSwiftMembershipSearch, OnSearchClicked); searchButton.AutomationId = "membership-grant-search";
    submitButton = LocalizedButton(UiMessageKey.NativeSwiftMembershipMembershipGrant, OnSubmitClicked); submitButton.AutomationId = "membership-grant-submit";
    retryButton = LocalizedButton(UiMessageKey.NativeSwiftMembershipRetry, OnRetryClicked); retryButton.AutomationId = "membership-grant-retry";
    results.ItemTemplate = new DataTemplate(() => UserButton());
    results.SetBinding(ItemsView.ItemsSourceProperty, nameof(MembershipGrantViewModel.Results));
    Content = new ScrollView { Content = BuildLayout() };
    Refresh();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await viewModel.LoadPlansAsync().ConfigureAwait(true);
    Refresh();
  }

  private View BuildLayout() => new VerticalStackLayout
  {
    Padding = 16, Spacing = 10,
    Children =
    {
      UiCopy.Bind(new Label { FontSize = 24, FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeSwiftMembershipMembershipGrant),
      UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeSwiftMembershipMembershipGrantDescription),
      query,
      searchButton,
      results,
      UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeSwiftMembershipSelectedUser),
      selectedUser,
      UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeSwiftMembershipSelectPlan), plan,
      planStatus,
      UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeSwiftMembershipSelectSku), sku,
      UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeSwiftSettingsSelectedDays), durationDays,
      noSkus,
      retryButton,
      submitButton,
      errorMessage,
      successMessage,
    },
  };

  private Button UserButton()
  {
    var button = new Button();
    button.BindingContextChanged += (_, _) => button.Text = button.BindingContext is UserSearchResult user
        ? UserLabel(user) : string.Empty;
    button.Clicked += (_, _) => { if (button.BindingContext is UserSearchResult user) { viewModel.SelectUser(user); Refresh(); } };
    return button;
  }

  private static Button LocalizedButton(UiMessageKey key, EventHandler action)
  {
    var button = UiCopy.Bind(new Button(), Button.TextProperty, key); button.Clicked += action; return button;
  }

  private async void OnSearchClicked(object? sender, EventArgs args) =>
      await viewModel.SearchAsync().ConfigureAwait(true);

  private async void OnSubmitClicked(object? sender, EventArgs args) =>
      await viewModel.GrantAsync().ConfigureAwait(true);

  private async void OnRetryClicked(object? sender, EventArgs args) =>
      await viewModel.LoadPlansAsync().ConfigureAwait(true);

  private void OnPlanSelected(object? sender, EventArgs args)
  {
    if (!isSynchronizingSelection) viewModel.SelectedPlan = (plan.SelectedItem as MembershipGrantPlanOption)?.Value;
  }

  private void OnSkuSelected(object? sender, EventArgs args)
  {
    if (!isSynchronizingSelection) viewModel.SelectedSku = (sku.SelectedItem as MembershipGrantSkuOption)?.Value;
  }

  private void OnChanged(object? sender, PropertyChangedEventArgs _) => Refresh();

  public void OnUiLocaleChanged()
  {
    Title = localization.Localize(UiMessageKey.NativeSwiftMembershipMembershipGrants);
    Refresh();
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    viewModel.PropertyChanged -= OnChanged;
    localeSubscription.Dispose();
  }


  private void Refresh()
  {
    isSynchronizingSelection = true;
    try
    {
    var planOptions = new[]
    {
      new MembershipGrantPlanOption(MembershipGrantPlanSlug.Plus, localization.Localize(UiMessageKey.NativeSwiftMembershipPlus)),
      new MembershipGrantPlanOption(MembershipGrantPlanSlug.Pro, localization.Localize(UiMessageKey.NativeSwiftMembershipPro)),
    };
    plan.ItemsSource = planOptions;
    plan.ItemDisplayBinding = new Binding(nameof(MembershipGrantPlanOption.Label));
    plan.SelectedItem = planOptions.FirstOrDefault(option => option.Value == viewModel.SelectedPlan);
    var skuOptions = viewModel.AvailableSkus.Select(value => MembershipGrantSkuOption.Create(value, localization)).ToArray();
    sku.ItemsSource = skuOptions;
    sku.ItemDisplayBinding = new Binding(nameof(MembershipGrantSkuOption.Label));
    sku.SelectedItem = skuOptions.FirstOrDefault(option => option.Value == viewModel.SelectedSku);
    }
    finally
    {
      isSynchronizingSelection = false;
    }
    errorMessage.Text = viewModel.LocalizedError ?? string.Empty;
    successMessage.Text = viewModel.Success is { } text ? localization.Resolve(text) : string.Empty;
    searchButton.IsEnabled = !viewModel.IsSearching && !string.IsNullOrWhiteSpace(viewModel.Query);
    submitButton.IsEnabled = viewModel.CanSubmit;
    submitButton.SetDynamicResource(Button.TextProperty, viewModel.IsSubmitting
        ? UiMessageKey.NativeSwiftMembershipSubmitting.Value
        : UiMessageKey.NativeSwiftMembershipMembershipGrant.Value);
    retryButton.IsVisible = !viewModel.IsPlansLoading && viewModel.PlanError is not null;
    planStatus.Text = viewModel.IsPlansLoading ? localization.Localize(UiMessageKey.NativeDotnetResidualLoading) : string.Empty;
    noSkus.Text = viewModel.SelectedPlan is not null && viewModel.AvailableSkus.Count == 0
        ? localization.Localize(UiMessageKey.NativeSwiftMembershipNoSkusForPlan) : string.Empty;
    selectedUser.Text = viewModel.SelectedUser is { } user ? UserLabel(user) : string.Empty;
  }

  private static string UserLabel(UserSearchResult user) =>
      string.IsNullOrWhiteSpace(user.Username) ? user.Id : user.Username;
}
