using System.ComponentModel;
using Voucha.Client.Core.FeatureFlags;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class FeatureFlagOverridesPage : ContentPage
{
  private readonly FeatureFlagOverridesViewModel viewModel;

  public FeatureFlagOverridesPage(FeatureFlagOverridesViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    viewModel.PropertyChanged += OnViewModelChanged;
    Render();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await viewModel.LoadAsync().ConfigureAwait(true);
    Render();
  }

  private async void OnClearOverridesClicked(object? sender, EventArgs e) =>
      await viewModel.ClearOverridesAsync().ConfigureAwait(true);

  private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => Render();

  private void Render()
  {
    LoadingIndicator.IsRunning = viewModel.IsLoading;
    LoadingIndicator.IsVisible = viewModel.IsLoading;
    ErrorPanel.IsVisible = viewModel.HasError;
    ErrorLabel.Text = viewModel.ErrorMessage;
    AccessRequiredLabel.IsVisible = !viewModel.IsAuthorized;
    ClearOverridesButton.IsVisible = viewModel.IsAuthorized;
    ClearOverridesButton.IsEnabled = viewModel.CanMutate && viewModel.HasLocalOverrides;
    OverridesLayout.Children.Clear();
    foreach (var flag in viewModel.Flags) OverridesLayout.Children.Add(CreateOverrideCard(flag));
  }

  private View CreateOverrideCard(FeatureFlagOverrideItem flag)
  {
    var picker = new Picker
    {
      AutomationId = $"feature-flag-{flag.Key}-override",
      Title = UiCopy.Format(
          UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsDeviceOverride,
          ("key", UiText.ProtocolValue(flag.Key))),
      ItemsSource = new[]
      {
        UiCopy.Localize(UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsInherited),
        UiCopy.Localize(UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsEnabled),
        UiCopy.Localize(UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsDisabled),
      },
      IsEnabled = viewModel.CanMutate,
      SelectedIndex = OverrideIndex(flag.LocalOverride),
    };
    var isReconciling = false;
    picker.SelectedIndexChanged += async (_, _) =>
    {
      if (isReconciling) return;
      var succeeded = picker.SelectedIndex switch
      {
        0 => await viewModel.RemoveOverrideAsync(flag.Key).ConfigureAwait(true),
        1 => await viewModel.SetOverrideAsync(flag.Key, true).ConfigureAwait(true),
        2 => await viewModel.SetOverrideAsync(flag.Key, false).ConfigureAwait(true),
        _ => false,
      };
      if (succeeded) return;
      isReconciling = true;
      picker.SelectedIndex = OverrideIndex(viewModel.Flags.Single(item => item.Key == flag.Key).LocalOverride);
      isReconciling = false;
    };
    return new VerticalStackLayout
    {
      AutomationId = $"feature-flag-{flag.Key}",
      Spacing = 4,
      Children =
      {
        new Label { Text = flag.Key, FontAttributes = FontAttributes.Bold },
        new Label
        {
          Text = UiCopy.Format(
              UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsGlobalEffectiveValue,
              ("global", LocalizedBoolean(flag.GlobalValue)),
              ("effective", LocalizedBoolean(flag.EffectiveValue))),
        },
        picker,
      },
    };
  }

  private static int OverrideIndex(bool? value) => value switch { true => 1, false => 2, null => 0 };
  private static UiText LocalizedBoolean(bool value) => UiText.Localized(value
      ? UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsEnabled
      : UiMessageKey.NativeSwiftDynamicConfigFeatureFlagsDisabled);
}
