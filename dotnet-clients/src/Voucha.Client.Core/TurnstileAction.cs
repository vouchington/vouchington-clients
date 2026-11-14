namespace Voucha.Client.Core;

public static class TurnstileAction
{
  public static string FromRouteAction(string routeAction)
  {
    ArgumentNullException.ThrowIfNull(routeAction);
    var action = routeAction.Replace('.', '-');
    if (action.Length is < 1 or > 32 ||
        action.Any(character => character is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or
            >= '0' and <= '9' or '_' or '-')))
    {
      throw new InvalidOperationException("Invalid Turnstile route action.");
    }

    return action;
  }
}
