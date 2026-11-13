using Voucha.Client.Core.Posts;

namespace Voucha.Client.App.Controls;

public partial class PostTimeRangePicker : ContentView
{
  public static readonly BindableProperty SelectionProperty = BindableProperty.Create(
      nameof(Selection), typeof(PostTimeRangeSelection), typeof(PostTimeRangePicker),
      propertyChanged: (bindable, _, value) =>
          ((PostTimeRangePicker)bindable).RangePicker.BindingContext = value);

  public PostTimeRangePicker() => InitializeComponent();

  public PostTimeRangeSelection? Selection
  {
    get => (PostTimeRangeSelection?)GetValue(SelectionProperty);
    set => SetValue(SelectionProperty, value);
  }

  public event EventHandler<string>? RangeSelected;

  private void OnSelectedIndexChanged(object? sender, EventArgs e)
  {
    if (RangePicker.SelectedItem is PostTimeRangeOption option &&
        Selection is { } selection && option.Value != selection.SelectedOption.Value)
      RangeSelected?.Invoke(this, option.Value);
  }
}
