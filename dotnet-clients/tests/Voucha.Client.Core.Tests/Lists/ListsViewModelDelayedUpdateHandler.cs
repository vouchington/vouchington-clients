using Voucha.Client.Core.Tests.Api;

namespace Voucha.Client.Core.Tests.Lists;

public sealed partial class ListsViewModelTests
{
  private sealed class DelayedUpdateHandler : HttpMessageHandler
  {
    internal TaskCompletionSource PatchStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal TaskCompletionSource ReleasePatch { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var responseBody = request.RequestUri?.PathAndQuery switch
      {
        "/api/v1/lists?limit=25" => TwoListsJson,
        "/api/v1/lists/list-1/items?limit=25" => ApiFixtureLoader.LoadResponse("native.list-items.default"),
        "/api/v1/lists/list-2/items?limit=25" => List2ItemsJson,
        "/api/v1/lists/list-1" when request.Method == HttpMethod.Patch => await DelayedPatchAsync(cancellationToken),
        _ => throw new InvalidOperationException(
            $"Unexpected HTTP {request.Method} {request.RequestUri?.PathAndQuery}.")
      };

      return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
      {
        Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json"),
        RequestMessage = request,
      };
    }

    private async Task<string> DelayedPatchAsync(CancellationToken cancellationToken)
    {
      PatchStarted.SetResult();
      await ReleasePatch.Task.WaitAsync(cancellationToken);
      return UpdatedListJson;
    }
  }
}
