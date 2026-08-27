using System.ComponentModel;
using Voucha.Client.Core.IdentityVerificationAdministration;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class IdentityVerificationAttemptGrantPage : ContentPage, IUiLocaleChangeListener, IDisposable
{
  private readonly IdentityVerificationAttemptGrantViewModel viewModel;
  private readonly IUiLocalization localization;
  private readonly Editor note = UiCopy.Bind(new Editor { AutomationId = "identity-verification-grant-note" }, Editor.PlaceholderProperty, UiMessageKey.NativeSwiftIdentityVerificationNote);
  private readonly Label status = new();
  private readonly Label loadError = new() { TextColor = Colors.IndianRed };
  private readonly Label submission = new();
  private readonly Button submitButton;
  private readonly IDisposable localeSubscription;
  private bool disposed;

  public IdentityVerificationAttemptGrantPage(
      IdentityVerificationAttemptGrantViewModel viewModel,
      IUiLocalization localization,
      IUiLocaleController localeController)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
    localeSubscription = (localeController ?? throw new ArgumentNullException(nameof(localeController)))
        .SubscribeLocaleChanges(this);
    BindingContext = viewModel;
    viewModel.PropertyChanged += OnChanged;
    note.SetBinding(Editor.TextProperty, nameof(IdentityVerificationAttemptGrantViewModel.Note), BindingMode.TwoWay);
    submitButton = UiCopy.Bind(new Button { AutomationId = "identity-verification-grant-submit" }, Button.TextProperty, UiMessageKey.NativeSwiftIdentityVerificationGrant);
    submitButton.Clicked += OnSubmitClicked;
    Content = new ScrollView { Content = BuildLayout() };
    Refresh();
  }

  public Task ApplyRouteAsync(string idOrUsername, bool isAdministrator)
  {
    viewModel.Configure(idOrUsername, isAdministrator);
    Refresh();
    return Task.CompletedTask;
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (viewModel.IsAdministrator) await viewModel.LoadAsync().ConfigureAwait(true);
    Refresh();
  }

  private View BuildLayout() => new VerticalStackLayout
  {
    Padding = 16, Spacing = 10,
    Children =
    {
      UiCopy.Bind(new Label { FontSize = 24, FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeSwiftIdentityVerificationTitle),
      UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeSwiftIdentityVerificationDescription),
      note,
      submitButton,
      loadError,
      submission,
      status,
    },
  };

  private async void OnSubmitClicked(object? sender, EventArgs args) =>
      await viewModel.GrantAsync().ConfigureAwait(true);

  private void OnChanged(object? sender, PropertyChangedEventArgs _) => Refresh();

  public void OnUiLocaleChanged()
  {
    Title = localization.Localize(
        viewModel.IsAdministrator
            ? UiMessageKey.NativeSwiftIdentityVerificationTitle
            : UiMessageKey.NativeSwiftIdentityVerificationAdministratorRequired);
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
    Title = localization.Localize(
        viewModel.IsAdministrator
            ? UiMessageKey.NativeSwiftIdentityVerificationTitle
            : UiMessageKey.NativeSwiftIdentityVerificationAdministratorRequired);
    var admin = viewModel.IsAdministrator;
    note.IsVisible = admin;
    submitButton.IsVisible = admin;
    submitButton.IsEnabled = viewModel.CanSubmit;
    submitButton.SetDynamicResource(
        Button.TextProperty,
        viewModel.IsSubmitting
            ? UiMessageKey.NativeSwiftIdentityVerificationGranting.Value
            : UiMessageKey.NativeSwiftIdentityVerificationGrant.Value);
    loadError.Text = viewModel.LoadError is { } error ? localization.Resolve(error) : string.Empty;
    submission.Text = viewModel.Submission is { } text ? localization.Resolve(text) : string.Empty;
    submission.TextColor = viewModel.Submission?.Key == UiMessageKey.NativeSwiftIdentityVerificationSuccess
        ? Colors.ForestGreen
        : Colors.IndianRed;
    status.Text = admin
        ? string.Empty
        : localization.Localize(UiMessageKey.NativeSwiftIdentityVerificationAdministratorMessage);
  }
}
