using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.NewsFeeds;

internal static class StoryDiscussionApiError
{
  public static bool IsFeedNotDiscoverable(VouchaApiException exception) =>
      exception.StatusCode == HttpStatusCode.Forbidden &&
      string.Equals(ReadCode(exception.ResponseBody), "FEED_NOT_DISCOVERABLE", StringComparison.Ordinal);

  public static string? ContributionAdmissionMessage(
      VouchaApiException exception,
      IUiLocalization localization) =>
      exception.StatusCode == HttpStatusCode.Conflict ? exception.ErrorCode switch
      {
        "CONTRIBUTION_ADMISSION_IN_PROGRESS" => localization.Localize(
            UiMessageKey.NativeTaxonomyContributionAdmissionInProgress),
        "IDEMPOTENCY_KEY_REUSED" => localization.Localize(
            UiMessageKey.NativeTaxonomyContributionAdmissionIdempotencyMismatch),
        _ => null,
      } : null;

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
