namespace Voucha.Client.App.Controls;

public sealed partial class NativeHtmlContentView
{
  public static readonly BindableProperty IsRightToLeftProperty = BindableProperty.Create(
      nameof(IsRightToLeft), typeof(bool?), typeof(NativeHtmlContentView), propertyChanged: OnContentChanged);

  public bool? IsRightToLeft { get => (bool?)GetValue(IsRightToLeftProperty); set => SetValue(IsRightToLeftProperty, value); }

  private void ApplyAuthoredLanguage(Label label)
  {
    if (IsRightToLeft is { } rightToLeft)
      label.FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
  }
}
