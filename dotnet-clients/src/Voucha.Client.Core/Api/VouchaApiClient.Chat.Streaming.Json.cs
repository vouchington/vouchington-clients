using System.Text.Json;

namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  private static string ParseChatError(string rawData)
  {
    if (TryGetObject(rawData, out var error) && TryGetString(error, "error", out var message))
    {
      return message;
    }

    return string.IsNullOrWhiteSpace(rawData) ? "Unknown error" : rawData;
  }

  private static bool TryGetObject(string rawData, out JsonElement element)
  {
    try
    {
      using var document = JsonDocument.Parse(rawData);
      if (document.RootElement.ValueKind == JsonValueKind.Object)
      {
        element = document.RootElement.Clone();
        return true;
      }
    }
    catch (JsonException)
    {
    }

    element = default;
    return false;
  }

  private static bool TryGetString(JsonElement element, string propertyName, out string value)
  {
    if (element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String)
    {
      value = property.GetString() ?? string.Empty;
      return true;
    }

    value = string.Empty;
    return false;
  }

  private static bool TryGetJsonElement(JsonElement element, string propertyName, out JsonElement value)
  {
    if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
    {
      value = property.Clone();
      return true;
    }

    value = default;
    return false;
  }
}
