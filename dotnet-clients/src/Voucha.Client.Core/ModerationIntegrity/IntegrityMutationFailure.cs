using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.ModerationIntegrity;

public static class IntegrityMutationFailure
{
  public static bool IsAmbiguous(Exception exception) => exception switch
  {
    OperationCanceledException => true,
    VouchaApiDecodeException => true,
    JsonException => true,
    HttpRequestException { StatusCode: null } => true,
    HttpRequestException { StatusCode: HttpStatusCode.RequestTimeout } => true,
    HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError } => true,
    _ => false,
  };

  public static bool IsExpected(Exception exception) =>
      exception is VouchaApiException or HttpRequestException or JsonException;
}
