namespace Voucha.Client.Core.Api;

public readonly record struct RssFeedEnabledFilter(string QueryValue)
{
  public static RssFeedEnabledFilter Enabled { get; } = new("true");

  public static RssFeedEnabledFilter Disabled { get; } = new("false");

  public static RssFeedEnabledFilter All { get; } = new("null");

  public static RssFeedEnabledFilter FromBoolean(bool enabled) => enabled ? Enabled : Disabled;

  public static implicit operator RssFeedEnabledFilter(bool enabled) => enabled ? Enabled : Disabled;
}
