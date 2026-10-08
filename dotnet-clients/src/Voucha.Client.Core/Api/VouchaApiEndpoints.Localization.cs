namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest Localization(
      string consumer,
      string locales,
      string selectors,
      string? etag = null)
  {
    var request = Get(
        "/api/v1/localization",
        Query(("consumer", consumer), ("locales", locales), ("selectors", selectors)));
    if (string.IsNullOrWhiteSpace(etag)) return request;
    return request with
    {
      Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["If-None-Match"] = etag,
      },
    };
  }
}
