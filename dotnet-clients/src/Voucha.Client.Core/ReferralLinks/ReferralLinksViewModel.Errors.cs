using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ReferralLinks;

public sealed partial class ReferralLinksViewModel
{
  private static string MutationErrorMessage(Exception exception) =>
      exception is VouchaApiException { ResponseBody: { } responseBody } &&
      ExtractApiErrorText(responseBody) is { } errorText
          ? errorText
          : exception.Message;

  private static string? ExtractApiErrorText(string responseBody)
  {
    try
    {
      using var document = JsonDocument.Parse(responseBody);
      return ExtractApiErrorText(document.RootElement);
    }
    catch (JsonException)
    {
      return null;
    }
  }

  private static string? ExtractApiErrorText(JsonElement element)
  {
    if (element.ValueKind != JsonValueKind.Object)
    {
      return null;
    }

    foreach (var propertyName in new[] { "user_error_text", "message", "error", "detail", "title" })
    {
      if (element.TryGetProperty(propertyName, out var property) &&
          property.ValueKind == JsonValueKind.String &&
          !string.IsNullOrWhiteSpace(property.GetString()))
      {
        return property.GetString();
      }
    }

    return element.TryGetProperty("error", out var errorObject)
        ? ExtractApiErrorText(errorObject)
        : null;
  }
}
