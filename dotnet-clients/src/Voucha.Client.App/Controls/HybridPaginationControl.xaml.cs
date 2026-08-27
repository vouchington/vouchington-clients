using System.Windows.Input;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Controls;

public partial class HybridPaginationControl : ContentView
{
  public static readonly BindableProperty HasMoreProperty = BindableProperty.Create(
      nameof(HasMore), typeof(bool), typeof(HybridPaginationControl), true,
      propertyChanged: OnPresentationChanged);
  public static readonly BindableProperty IsLoadingProperty = BindableProperty.Create(
      nameof(IsLoading), typeof(bool), typeof(HybridPaginationControl), false,
      propertyChanged: OnLoadingChanged);
  public static readonly BindableProperty HasErrorProperty = BindableProperty.Create(
      nameof(HasError), typeof(bool), typeof(HybridPaginationControl), false,
      propertyChanged: OnPresentationChanged);
  public static readonly BindableProperty LoadNextPageCommandProperty = BindableProperty.Create(
      nameof(LoadNextPageCommand), typeof(ICommand), typeof(HybridPaginationControl));
  public static readonly BindableProperty PaginationIdProperty = BindableProperty.Create(
      nameof(PaginationId), typeof(string), typeof(HybridPaginationControl), null,
      propertyChanged: OnPaginationIdChanged);

  public HybridPaginationControl()
  {
    InitializeComponent();
    UpdatePresentation();
  }

  public event EventHandler? LoadNextPageRequested;
  public event EventHandler? LoadingCompleted;

  public bool HasMore
  {
    get => (bool)GetValue(HasMoreProperty);
    set => SetValue(HasMoreProperty, value);
  }

  public bool IsLoading
  {
    get => (bool)GetValue(IsLoadingProperty);
    set => SetValue(IsLoadingProperty, value);
  }

  public bool HasError
  {
    get => (bool)GetValue(HasErrorProperty);
    set => SetValue(HasErrorProperty, value);
  }

  public ICommand? LoadNextPageCommand
  {
    get => (ICommand?)GetValue(LoadNextPageCommandProperty);
    set => SetValue(LoadNextPageCommandProperty, value);
  }

  public string? PaginationId
  {
    get => (string?)GetValue(PaginationIdProperty);
    set => SetValue(PaginationIdProperty, value);
  }

  public void TryLoadAutomatically()
  {
    if (!HasMore || IsLoading || HasError) return;
    RequestNextPage();
  }

  private void OnActionClicked(object? sender, EventArgs eventArgs) => RequestNextPage();

  private void RequestNextPage()
  {
    if (!HasMore || IsLoading) return;
    if (LoadNextPageCommand?.CanExecute(null) == true) LoadNextPageCommand.Execute(null);
    else LoadNextPageRequested?.Invoke(this, EventArgs.Empty);
  }

  private static void OnPresentationChanged(BindableObject bindable, object oldValue, object newValue)
      => ((HybridPaginationControl)bindable).UpdatePresentation();

  private static void OnLoadingChanged(BindableObject bindable, object oldValue, object newValue)
  {
    var control = (HybridPaginationControl)bindable;
    control.UpdatePresentation();
    if (oldValue is true && newValue is false) control.LoadingCompleted?.Invoke(control, EventArgs.Empty);
  }

  private static void OnPaginationIdChanged(BindableObject bindable, object oldValue, object newValue) =>
      ((HybridPaginationControl)bindable).ApplyAutomationId(newValue as string);

  private void ApplyAutomationId(string? paginationId)
  {
    if (string.IsNullOrWhiteSpace(paginationId)) return;
    AutomationId = $"pagination-{paginationId}";
    ActionButton.AutomationId = $"pagination-{paginationId}-action";
  }

  private void UpdatePresentation()
  {
    var resourceKey = IsLoading
        ? UiMessageKey.NativeSwiftCommonLoadingMore
        : HasError ? UiMessageKey.NativeCommonRetry : UiMessageKey.NativeSwiftCommonLoadMore;
    ActionButton.SetDynamicResource(Button.TextProperty, resourceKey.Value);
    ActionButton.SetDynamicResource(SemanticProperties.DescriptionProperty, resourceKey.Value);
    ActionButton.IsVisible = HasMore;
    ActionButton.IsEnabled = !IsLoading;
  }
}
