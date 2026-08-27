using System.Net;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiExceptionTests
{
  [Fact]
  public void ParsesTypedErrorCodeFromApiPayload()
  {
    var exception = new VouchaApiException(
        HttpStatusCode.Forbidden,
        "{\"error\":{\"code\":\"EMAIL_VERIFICATION_REQUIRED\",\"message\":\"Verify an email address.\"}}");

    Assert.Equal("EMAIL_VERIFICATION_REQUIRED", exception.ErrorCode);
    Assert.Equal("Verify an email address.", exception.ApiMessage);
    Assert.True(exception.IsEmailVerificationRequired);
  }

  [Fact]
  public void InvalidPayloadDoesNotProduceTypedError()
  {
    var exception = new VouchaApiException(HttpStatusCode.BadRequest, "not-json");

    Assert.Null(exception.ErrorCode);
    Assert.Null(exception.ApiMessage);
    Assert.False(exception.IsEmailVerificationRequired);
  }

  [Fact]
  public void ConstructorPreservesMessageInnerExceptionAndStatusCode()
  {
    var innerException = new InvalidOperationException("inner");

    var exception = new VouchaApiException(
        "Request failed.",
        innerException,
        HttpStatusCode.TooManyRequests);

    Assert.Equal("Request failed.", exception.Message);
    Assert.Same(innerException, exception.InnerException);
    Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
    Assert.Null(exception.ResponseBody);
  }
}
