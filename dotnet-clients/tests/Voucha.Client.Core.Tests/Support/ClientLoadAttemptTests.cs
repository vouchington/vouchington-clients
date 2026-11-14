using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class ClientLoadAttemptTests
{
  [Fact]
  public async Task SuccessfulWorkDoesNotReportCancellationOrAnError()
  {
    var ran = false;
    var canceled = 0;
    var errors = new List<string>();

    await ClientLoadAttempt.RunAsync(
        () =>
        {
          ran = true;
          return Task.CompletedTask;
        },
        () => canceled++,
        errors.Add);

    Assert.True(ran);
    Assert.Equal(0, canceled);
    Assert.Empty(errors);
  }

  [Fact]
  public async Task CancellationReportsTheCancelCallbackOnly()
  {
    var canceled = 0;
    var errors = new List<string>();

    await ClientLoadAttempt.RunAsync(
        () => Task.FromException(new TaskCanceledException("request canceled")),
        () => canceled++,
        errors.Add);

    Assert.Equal(1, canceled);
    Assert.Empty(errors);
  }

  [Theory]
  [MemberData(nameof(ExpectedFailures))]
  public async Task ExpectedClientFailuresReportTheExceptionMessage(Exception failure)
  {
    var canceled = 0;
    var errors = new List<string>();

    await ClientLoadAttempt.RunAsync(
        () => Task.FromException(failure),
        () => canceled++,
        errors.Add);

    Assert.Equal(0, canceled);
    Assert.Equal([failure.Message], errors);
  }

  [Fact]
  public async Task UnexpectedExceptionsPropagateWithoutACallback()
  {
    var canceled = 0;
    var errors = new List<string>();
    var failure = new FormatException("unreadable payload");

    var thrown = await Assert.ThrowsAsync<FormatException>(() => ClientLoadAttempt.RunAsync(
        () => Task.FromException(failure),
        () => canceled++,
        errors.Add));

    Assert.Same(failure, thrown);
    Assert.Equal(0, canceled);
    Assert.Empty(errors);
  }

  [Fact]
  public async Task MissingWorkIsRejectedBeforeTheCallbacks()
  {
    var canceled = 0;

    var thrown = await Assert.ThrowsAsync<ArgumentNullException>(() => ClientLoadAttempt.RunAsync(
        null!,
        () => canceled++,
        _ => throw new Xunit.Sdk.XunitException("An error callback must not run for a missing load.")));

    Assert.Equal("work", thrown.ParamName);
    Assert.Equal(0, canceled);
  }

  [Fact]
  public async Task MissingCallbacksAreRejectedBeforeTheWorkRuns()
  {
    var ran = false;

    var missingCancel = await Assert.ThrowsAsync<ArgumentNullException>(() => ClientLoadAttempt.RunAsync(
        () =>
        {
          ran = true;
          return Task.CompletedTask;
        },
        null!,
        _ => { }));
    var missingError = await Assert.ThrowsAsync<ArgumentNullException>(() => ClientLoadAttempt.RunAsync(
        () =>
        {
          ran = true;
          return Task.CompletedTask;
        },
        () => { },
        null!));

    Assert.Equal("onCanceled", missingCancel.ParamName);
    Assert.Equal("onError", missingError.ParamName);
    Assert.False(ran);
  }

  public static TheoryData<Exception> ExpectedFailures()
  {
    var failures = new TheoryData<Exception>
    {
      new VouchaApiException(HttpStatusCode.BadGateway, "upstream response body"),
      new HttpRequestException("offline", new TaskCanceledException("timed out")),
      new InvalidOperationException("response was empty"),
    };
    return failures;
  }
}
