namespace Voucha.Client.Core;

public sealed record SentryLaunchPlan(
    string? Dsn,
    string ReleaseName,
    string EnvironmentName,
    string? AccountId,
    bool SendDefaultPii,
    double TracesSampleRate)
{
  public const double EnabledTracesSampleRate = 0.1;

  public bool StartsSdk => Dsn is not null;

  public static SentryLaunchPlan Resolve(
      IReadOnlyDictionary<string, string?>? environment,
      IReadOnlyDictionary<string, string?>? packagedConfiguration,
      bool releaseConfiguration,
      string releaseName,
      string? accountId)
  {
    var dsn = ValidDsn(Trimmed(
        Read(environment, "VOUCHA_SENTRY_DSN") ?? Read(packagedConfiguration, "VOUCHA_SENTRY_DSN")));
    return new SentryLaunchPlan(
        dsn,
        releaseName,
        releaseConfiguration ? "release" : "debug",
        Trimmed(accountId),
        SendDefaultPii: false,
        dsn is null ? 0 : EnabledTracesSampleRate);
  }

  private static string? Read(IReadOnlyDictionary<string, string?>? values, string key)
  {
    if (values is null || !values.TryGetValue(key, out var value))
    {
      return null;
    }

    return value;
  }

  private static string? ValidDsn(string? value)
  {
    if (value is null || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        string.IsNullOrWhiteSpace(uri.UserInfo) ||
        string.IsNullOrWhiteSpace(uri.UserInfo.Split(':')[0]))
    {
      return null;
    }

    var lastSlash = uri.AbsoluteUri.LastIndexOf('/');
    if (lastSlash < 0 || string.IsNullOrWhiteSpace(uri.AbsoluteUri[(lastSlash + 1)..]))
    {
      return null;
    }

    return value;
  }

  private static string? Trimmed(string? value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrEmpty(trimmed) ? null : trimmed;
  }
}
