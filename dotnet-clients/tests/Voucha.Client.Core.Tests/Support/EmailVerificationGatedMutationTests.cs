using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class EmailVerificationGatedMutationTests
{
  [Fact]
  public void IsRequiredRecognizesOnlyVerificationFailures()
  {
    Assert.True(EmailVerificationRecovery.IsRequired(VerificationException()));
    Assert.False(EmailVerificationRecovery.IsRequired(new HttpRequestException("Offline.")));
  }

  [Fact]
  public async Task RunAsyncReturnsTypedResultOnSuccess()
  {
    var gate = new EmailVerificationGatedMutation();

    var result = await gate.RunAsync<string>(
        () => Task.FromResult("published"),
        _ => throw new Xunit.Sdk.XunitException("Successful mutations must not request recovery."));

    Assert.Equal("published", result);
    Assert.False(await gate.ConsumeRecoveryRequestAsync(
        _ => Task.FromException(new Xunit.Sdk.XunitException("No recovery request expected.")),
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task RunAsyncInvokesFallbackAndReturnsNormallyForVerificationFailure()
  {
    var gate = new EmailVerificationGatedMutation();
    var expected = VerificationException();
    var recoveryCount = 0;

    await gate.RunAsync(
        () => Task.FromException(expected),
        exception =>
        {
          Assert.Same(expected, exception);
          Assert.False(gate.ConsumeRecoveryRequestAsync(
              _ => Task.CompletedTask,
              TestContext.Current.CancellationToken).GetAwaiter().GetResult());
          recoveryCount++;
        });

    Assert.Equal(1, recoveryCount);
    Assert.True(await gate.ConsumeRecoveryRequestAsync(
        request =>
        {
          Assert.Same(expected, request.Error);
          return Task.CompletedTask;
        },
        TestContext.Current.CancellationToken));
    Assert.False(await gate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task RunAsyncPropagatesOtherFailuresWithoutRecoveryOrRollback()
  {
    var gate = new EmailVerificationGatedMutation();
    var expected = new HttpRequestException("Offline.");
    var rollbackCount = 0;

    var actual = await Assert.ThrowsAsync<HttpRequestException>(() => gate.RunAsync(
        () => Task.FromException(expected),
        _ => rollbackCount++));

    Assert.Same(expected, actual);
    Assert.Equal(0, rollbackCount);
    Assert.False(await gate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task RunAsyncPropagatesNonVerificationApiFailureWithoutRecoveryOrCallback()
  {
    var gate = new EmailVerificationGatedMutation();
    var expected = new VouchaApiException(
        HttpStatusCode.Forbidden,
        "{\"code\":\"FEED_NOT_DISCOVERABLE\"}");
    var callbackCount = 0;

    var actual = await Assert.ThrowsAsync<VouchaApiException>(() => gate.RunAsync(
        () => Task.FromException(expected),
        _ => callbackCount++));

    Assert.Same(expected, actual);
    Assert.Equal(0, callbackCount);
    Assert.False(await gate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task RunAsyncPropagatesCancellationWithoutRecoveryOrRollback()
  {
    var gate = new EmailVerificationGatedMutation();
    var expected = new OperationCanceledException(TestContext.Current.CancellationToken);
    var rollbackCount = 0;

    var actual = await Assert.ThrowsAsync<OperationCanceledException>(() => gate.RunAsync(
        () => Task.FromException(expected),
        _ => rollbackCount++));

    Assert.Same(expected, actual);
    Assert.Equal(0, rollbackCount);
    Assert.False(await gate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task PendingRecoveryIsMonotonicAndConsumedOnce()
  {
    var gate = new EmailVerificationGatedMutation();
    var first = VerificationException("first");
    var second = VerificationException("second");

    await gate.RunAsync(
        () => Task.FromException(first),
        _ => { });
    await gate.RunAsync(
        () => Task.FromException(second),
        _ => { });

    Assert.True(await gate.ConsumeRecoveryRequestAsync(
        request =>
        {
          Assert.Same(first, request.Error);
          return Task.CompletedTask;
        },
        TestContext.Current.CancellationToken));
    Assert.False(await gate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ConsumingRecoveryNeverReplaysTheMutation()
  {
    var gate = new EmailVerificationGatedMutation();
    var attempts = 0;

    Task Mutate()
    {
      attempts++;
      return attempts == 1 ? Task.FromException(VerificationException()) : Task.CompletedTask;
    }

    await gate.RunAsync(Mutate, _ => { });
    Assert.True(await gate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
    Assert.Equal(1, attempts);

    await gate.RunAsync(Mutate, _ => { });
    Assert.Equal(2, attempts);
  }

  [Fact]
  public async Task GenericRunAsyncReturnsFallbackAndStoresRequestAfterCallback()
  {
    var gate = new EmailVerificationGatedMutation();
    var expected = VerificationException();
    var requestWasPendingDuringCallback = false;

    var result = await gate.RunAsync(
        () => Task.FromException<string>(expected),
        Fallback);

    Assert.Equal("preserved", result);
    Assert.False(requestWasPendingDuringCallback);
    Assert.True(await gate.ConsumeRecoveryRequestAsync(
        request =>
        {
          Assert.Same(expected, request.Error);
          return Task.CompletedTask;
        },
        TestContext.Current.CancellationToken));

    string Fallback(VouchaApiException exception)
    {
      Assert.Same(expected, exception);
      requestWasPendingDuringCallback = gate.ConsumeRecoveryRequestAsync(
          _ => Task.CompletedTask,
          TestContext.Current.CancellationToken).GetAwaiter().GetResult();
      return "preserved";
    }
  }

  [Fact]
  public async Task ConsumeRecoveryRequestAtomicallyTakesBeforeInvokingConsumer()
  {
    var gate = new EmailVerificationGatedMutation();
    await gate.RunAsync(() => Task.FromException(VerificationException()), _ => { });
    var consumerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseConsumer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    var firstConsume = gate.ConsumeRecoveryRequestAsync(
        async _ =>
        {
          consumerStarted.SetResult();
          await releaseConsumer.Task;
        },
        TestContext.Current.CancellationToken);
    await consumerStarted.Task;

    Assert.False(await gate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
    releaseConsumer.SetResult();
    Assert.True(await firstConsume);
  }

  private static VouchaApiException VerificationException(string message = "Verify email.") =>
      new(HttpStatusCode.Forbidden, $"{{\"code\":\"EMAIL_VERIFICATION_REQUIRED\",\"message\":\"{message}\"}}");
}
