namespace Voucha.Client.App;

internal readonly record struct CatalystGuiLaunchOptions(bool IsActive, bool OpenNotifications)
{
  internal const string NotificationArgument = "--ui-testing-launch-smoke";
  internal const string RootShellArgument = "--ui-testing-root-shell-smoke";

  internal static CatalystGuiLaunchOptions FromArguments(IReadOnlyList<string> arguments)
  {
    ArgumentNullException.ThrowIfNull(arguments);
    if (arguments.Contains(RootShellArgument)) return new(true, false);
    return arguments.Contains(NotificationArgument) ? new(true, true) : new(false, false);
  }
}
