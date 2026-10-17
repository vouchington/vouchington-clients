using Voucha.Client.Core.Content;

namespace Voucha.Client.App.Controls;

public sealed partial class NativeHtmlContentView
{
  public static readonly BindableProperty IsRightToLeftProperty = BindableProperty.Create(
      nameof(IsRightToLeft), typeof(bool?), typeof(NativeHtmlContentView), propertyChanged: OnContentChanged);

  public static readonly BindableProperty DetectedLanguageProperty = BindableProperty.Create(
      nameof(DetectedLanguage), typeof(string), typeof(NativeHtmlContentView), propertyChanged: OnContentChanged);

  public bool? IsRightToLeft { get => (bool?)GetValue(IsRightToLeftProperty); set => SetValue(IsRightToLeftProperty, value); }

  public string? DetectedLanguage { get => (string?)GetValue(DetectedLanguageProperty); set => SetValue(DetectedLanguageProperty, value); }

  private void ApplyAuthoredLanguage(Label label)
  {
    var language = AuthoredContentLanguage.Resolve(null, DetectedLanguage);
    var direction = language.Direction is { } resolved
        ? resolved == AuthoredTextDirection.RightToLeft
        : IsRightToLeft;
    if (direction is { } rightToLeft)
      label.FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    label.HandlerChanged += (_, _) => ApplyPlatformLanguage(label, language.Tag);
    ApplyPlatformLanguage(label, language.Tag);
  }

  private static void ApplyPlatformLanguage(Label label, string? tag)
  {
#if MACCATALYST
    if (label.Handler?.PlatformView is UIKit.UILabel native) native.AccessibilityLanguage = tag;
#elif WINDOWS
    if (tag is not null && label.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement native) native.Language = tag;
#endif
  }
}
