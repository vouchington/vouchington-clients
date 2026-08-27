using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal static class IntegrityPageViews
{
  public static View SectionNavigation(string flagsPath, string penaltiesPath, string prefix)
  {
    var flags = NavigationButton(
        UiMessageKey.NativeSwiftIntegrityFlags, flagsPath, $"{prefix}-flags-tab");
    var penalties = NavigationButton(
        UiMessageKey.NativeSwiftIntegrityPenalties, penaltiesPath, $"{prefix}-penalties-tab");
    return new HorizontalStackLayout { Spacing = 8, Children = { flags, penalties } };
  }

  public static VerticalStackLayout CardLines(params string[] lines)
  {
    var stack = new VerticalStackLayout { Spacing = 5 };
    foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
      stack.Children.Add(new Label { Text = line });
    if (stack.Children.FirstOrDefault() is Label title)
      title.FontAttributes = FontAttributes.Bold;
    return stack;
  }

  public static View Entity(IntegrityEntityTarget target)
  {
    if (target.Route is null) return new Label { Text = UiCopy.Resolve(target.Label) };
    var button = new Button
    {
      Text = UiCopy.Resolve(target.Label),
      CommandParameter = target.Route,
      AutomationId = $"integrity-entity-{target.Id}",
    };
    button.Clicked += async (_, _) =>
    {
      if (Shell.Current is AppShell appShell)
        await appShell.OpenNativePathAsync(target.Route).ConfigureAwait(true);
    };
    return button;
  }

  public static void AddPair(VerticalStackLayout content, UiMessageKey key, string value)
  {
    content.Children.Add(new Label { Text = UiCopy.Localize(key) });
    content.Children.Add(new Label { Text = value });
  }

  public static string FlagType(string value) => value switch
  {
    "mass_report_suspected" =>
        UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityMassReportSuspected),
    "velocity_spike" => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityVelocitySpike),
    "ip_correlation" => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityIpCorrelation),
    _ => value,
  };

  public static string Resolution(string value) => value switch
  {
    "dismissed" => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityDismissedResolution),
    "penalized" => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityPenalizedResolution),
    "suspended" => UiCopy.Localize(UiMessageKey.NativeSwiftIntegritySuspendedResolution),
    _ => value,
  };

  public static Border Card(View content, string automationId) => new()
  {
    Content = content,
    Padding = 12,
    Stroke = Colors.LightGray,
    AutomationId = automationId,
  };

  private static Button NavigationButton(UiMessageKey key, string path, string automationId)
  {
    var button = UiCopy.Bind(
        new Button { CommandParameter = path, AutomationId = automationId },
        Button.TextProperty, key);
    button.Clicked += async (_, _) =>
    {
      if (Shell.Current is AppShell appShell)
        await appShell.OpenNativePathAsync(path).ConfigureAwait(true);
    };
    return button;
  }
}
