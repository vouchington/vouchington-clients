using System.Net;
using Voucha.Client.Core.Agent;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed class MemberMcpLoopbackCallbackTests
{
  [Fact]
  public async Task ReceivesOneCallbackOnAnOsAssignedLoopbackPort()
  {
    await using var callback = MemberMcpLoopbackCallback.Start();
    Assert.Equal("http", callback.RedirectUri.Scheme);
    Assert.True(IPAddress.IsLoopback(IPAddress.Parse(callback.RedirectUri.Host)));
    Assert.InRange(callback.RedirectUri.Port, 1, 65535);
    Assert.Equal("/oauth/native/windows/callback", callback.RedirectUri.AbsolutePath);

    using var browser = new HttpClient();
    var receive = callback.ReceiveAsync(TestContext.Current.CancellationToken);
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => callback.ReceiveAsync(TestContext.Current.CancellationToken));
    var request = browser.GetAsync(
        new Uri(callback.RedirectUri + "?code=one%2Btwo&state=state-1&iss=https%3A%2F%2Fexample.test"),
        TestContext.Current.CancellationToken);
    try
    {
      var result = await receive;
      using var response = await request;
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      Assert.Contains("code=one%2Btwo", result.Query, StringComparison.Ordinal);
      await Assert.ThrowsAsync<InvalidOperationException>(
          () => callback.ReceiveAsync(TestContext.Current.CancellationToken));
    }
    finally
    {
      try { await receive; } catch (Exception) { }
      try { using var response = await request; } catch (Exception) { }
    }
  }

  [Fact]
  public async Task InvalidPathIsRejectedAndListenerCloses()
  {
    await using var callback = MemberMcpLoopbackCallback.Start();
    using var browser = new HttpClient();
    var receive = callback.ReceiveAsync(TestContext.Current.CancellationToken);
    var request = browser.GetAsync(new Uri(callback.RedirectUri, "/wrong?code=code-1"),
        TestContext.Current.CancellationToken);
    try
    {
      using var response = await request;
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      await Assert.ThrowsAsync<InvalidDataException>(() => receive);
    }
    finally
    {
      try { await receive; } catch (InvalidDataException) { }
      try { using var response = await request; } catch (Exception) { }
    }
  }

  [Fact]
  public async Task CancellationStopsThePendingListener()
  {
    await using var callback = MemberMcpLoopbackCallback.Start();
    using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var receive = callback.ReceiveAsync(cancel.Token);
    cancel.Cancel();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => receive);
  }
}
