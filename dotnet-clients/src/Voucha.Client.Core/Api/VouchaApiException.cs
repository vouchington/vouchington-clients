using System.Net;
using System.Text.Json;

namespace Voucha.Client.Core.Api;

public sealed class VouchaApiException : HttpRequestException
{
  public VouchaApiException()
  {
  }

  public VouchaApiException(string? message)
      : base(message)
  {
  }

  public VouchaApiException(string? message, Exception? innerException)
      : base(message, innerException)
  {
  }

  public VouchaApiException(string? message, Exception? innerException, HttpStatusCode? statusCode)
      : base(message, innerException, statusCode)
  {
  }

  public VouchaApiException(HttpStatusCode statusCode, string? responseBody, TimeSpan? retryAfter = null)
      : base($"Voucha API request failed with HTTP {(int)statusCode}.", null, statusCode)
  {
    ResponseBody = responseBody;
    RetryAfter = retryAfter;
    (ErrorCode, ApiMessage) = ParseError(responseBody);
  }

  public string? ResponseBody { get; }

  public TimeSpan? RetryAfter { get; }

  public string? ErrorCode { get; }

  public string? ApiMessage { get; }

  public bool IsEmailVerificationRequired =>
      string.Equals(ErrorCode, "EMAIL_VERIFICATION_REQUIRED", StringComparison.Ordinal);

  public bool IsTagLimitReached =>
      string.Equals(ErrorCode, "TAG_LIMIT_REACHED", StringComparison.Ordinal);

  private static (string? Code, string? Message) ParseError(string? responseBody)
  {
    if (string.IsNullOrWhiteSpace(responseBody)) return (null, null);
    try
    {
      using var document = JsonDocument.Parse(responseBody);
      var payload = document.RootElement;
      if (payload.TryGetProperty("error", out var nested) && nested.ValueKind == JsonValueKind.Object)
      {
        payload = nested;
      }

      return (StringProperty(payload, "code"), StringProperty(payload, "message"));
    }
    catch (JsonException)
    {
      return (null, null);
    }
  }

  private static string? StringProperty(JsonElement payload, string name) =>
      payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
          ? value.GetString()
          : null;
}
