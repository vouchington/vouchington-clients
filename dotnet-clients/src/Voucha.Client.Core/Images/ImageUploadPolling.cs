using System.Net;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Images;

public static class ImageUploadPolling
{
  public static bool IsTransientFailure(Exception exception) => exception switch
  {
    OperationCanceledException => false,
    VouchaApiException apiException when apiException.StatusCode is HttpStatusCode statusCode =>
        IsRetryableStatus(statusCode),
    HttpRequestException requestException when requestException.StatusCode is null => true,
    HttpRequestException requestException when requestException.StatusCode is HttpStatusCode statusCode =>
        IsRetryableStatus(statusCode),
    _ => false,
  };

  private static bool IsRetryableStatus(HttpStatusCode statusCode) =>
      (int)statusCode is 429 or >= 500 and <= 599;
}
