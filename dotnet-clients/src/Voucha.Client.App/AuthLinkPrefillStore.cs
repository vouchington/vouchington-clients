namespace Voucha.Client.App;

public sealed class AuthLinkPrefillStore
{
  private readonly object gate = new();
  private AuthLinkPrefill? prefill;

  public void Set(AuthLinkPrefill value)
  {
    lock (gate)
    {
      prefill = value;
    }
  }

  public AuthLinkPrefill? Take()
  {
    lock (gate)
    {
      var value = prefill;
      prefill = null;
      return value;
    }
  }
}

public sealed record AuthLinkPrefill(string? EmailAddress, string? Otp);
