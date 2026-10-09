using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Xunit;

namespace Voucha.Client.Core.Tests.Images;

public sealed class ImageUploadPollingTests
{
  [Theory]
  [InlineData(HttpStatusCode.TooManyRequests, true)]
  [InlineData(HttpStatusCode.InternalServerError, true)]
  [InlineData(HttpStatusCode.HttpVersionNotSupported, true)]
  [InlineData((HttpStatusCode)599, true)]
  [InlineData(HttpStatusCode.RequestTimeout, false)]
  [InlineData((HttpStatusCode)499, false)]
  [InlineData(HttpStatusCode.NotFound, false)]
  public void ApiStatusesRetryOnlyTooManyRequestsAndServerErrors(HttpStatusCode statusCode, bool retry)
  {
    var exception = new VouchaApiException(statusCode, responseBody: null);

    Assert.Equal(retry, ImageUploadPolling.IsTransientFailure(exception));
  }

  [Fact]
  public void TransportFailureWithoutStatusRetries()
  {
    var exception = new HttpRequestException("The connection closed before a response.");

    Assert.True(ImageUploadPolling.IsTransientFailure(exception));
  }

  [Fact]
  public void ApiFailureWithoutStatusRetries()
  {
    var exception = new VouchaApiException("The response ended before a status.");

    Assert.True(ImageUploadPolling.IsTransientFailure(exception));
  }

  [Theory]
  [InlineData(HttpStatusCode.TooManyRequests, true)]
  [InlineData(HttpStatusCode.BadGateway, true)]
  [InlineData(HttpStatusCode.NotFound, false)]
  public void TransportStatusesUseTheSameRetryRule(HttpStatusCode statusCode, bool retry)
  {
    var exception = new HttpRequestException("Upload polling failed.", inner: null, statusCode);

    Assert.Equal(retry, ImageUploadPolling.IsTransientFailure(exception));
  }

  [Fact]
  public void CancellationDoesNotRetry()
  {
    Assert.False(ImageUploadPolling.IsTransientFailure(new OperationCanceledException()));
    Assert.False(ImageUploadPolling.IsTransientFailure(new InvalidOperationException("blocked")));
  }
}
