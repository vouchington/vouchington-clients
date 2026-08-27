using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.NewsFeeds;

internal static class StoryDiscussionApiError
{
  public static bool IsFeedNotDiscoverable(VouchaApiException exception) =>
      exception.StatusCode == HttpStatusCode.Forbidden &&
      string.Equals(ReadCode(exception.ResponseBody), "FEED_NOT_DISCOVERABLE", StringComparison.Ordinal);

  private static string? ReadCode(string? responseBody)
  {
    if (string.IsNullOrWhiteSpace(responseBody)) return null;
    try
    {
      using var document = JsonDocument.Parse(responseBody);
      return document.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
          ? code.GetString()
          : null;
    }
    catch (JsonException)
    {
      return null;
    }
  }
}
