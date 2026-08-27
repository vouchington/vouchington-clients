using System.Net;
using System.Diagnostics;
using System.Text;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Search;
using Xunit;

namespace Voucha.Client.Core.Tests.Search;

public sealed partial class OmnisearchViewModelTests
{
  [Fact]
  public async Task LoadDomainsAsyncHonorsCallerCancellation()
  {
    var handler = new CancellableWebSearchHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

    var loadTask = viewModel.LoadDomainsAsync(cancellation.Token);
    await handler.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
    await cancellation.CancelAsync();
    await loadTask;

    Assert.True(handler.ObservedToken.IsCancellationRequested);
    Assert.Empty(viewModel.Groups);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task StaleWebSurfaceCompletionDoesNotReplaceCurrentGroups()
  {
    var handler = new DeferredWebSearchHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client);

    var domainsTask = viewModel.LoadDomainsAsync(TestContext.Current.CancellationToken);
    await handler.WaitForRequestCountAsync(1, TestContext.Current.CancellationToken);

    var urlsTask = viewModel.LoadUrlsAsync(TestContext.Current.CancellationToken);
    await handler.WaitForRequestCountAsync(2, TestContext.Current.CancellationToken);

    handler.CompleteRequest(0, HostnamesJson);
    handler.CompleteRequest(1, UrlsJson);
    await Task.WhenAll(domainsTask, urlsTask);

    Assert.Equal(OmnisearchWebSearchSurface.Urls, viewModel.ActiveSurface);
    Assert.Equal("URLs", Assert.Single(viewModel.Groups).Title);
    Assert.Equal(OmnisearchResultRoute.WebAddress("url-1"), Assert.Single(viewModel.Groups[0].Rows).Route);
  }

  private sealed class CancellableWebSearchHandler : HttpMessageHandler
  {
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CancellationToken ObservedToken { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      ObservedToken = cancellationToken;
      Started.SetResult();
      await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
      throw new UnreachableException();
    }
  }

  private sealed class DeferredWebSearchHandler : HttpMessageHandler
  {
    private readonly List<TaskCompletionSource<string>> completions = [];
    private readonly SemaphoreSlim requestsChanged = new(0);

    public void CompleteRequest(int index, string body) => completions[index].SetResult(body);

    public async Task WaitForRequestCountAsync(int count, CancellationToken cancellationToken)
    {
      while (completions.Count < count)
      {
        await requestsChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
      }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
      completions.Add(completion);
      requestsChanged.Release();
      var body = await completion.Task.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
        RequestMessage = request,
      };
    }
  }
}
