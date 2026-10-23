using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Analytics;

public interface IProductSurfaceAnalytics
{
  void RecordLandingPageVisit(string landingPageId, LandingPageVisitSignal signal);

  void RecordLandingPageClick(string landingPageId, LandingPageClickSignal signal);

  void RecordRssFeedView(string rssFeedId);

  void RecordRssFeedItemView(string itemId);
}

public sealed class SessionProductSurfaceAnalytics : IProductSurfaceAnalytics
{
  private readonly VouchaApiClient client;

  public SessionProductSurfaceAnalytics(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  internal Task LastDispatch { get; private set; } = Task.CompletedTask;

  public void RecordLandingPageVisit(string landingPageId, LandingPageVisitSignal signal) =>
      Dispatch(VouchaApiEndpoints.LandingPageVisit(landingPageId, signal));

  public void RecordLandingPageClick(string landingPageId, LandingPageClickSignal signal) =>
      Dispatch(VouchaApiEndpoints.LandingPageClick(landingPageId, signal));

  public void RecordRssFeedView(string rssFeedId) =>
      Dispatch(VouchaApiEndpoints.RssFeedView(rssFeedId));

  public void RecordRssFeedItemView(string itemId) =>
      Dispatch(VouchaApiEndpoints.RssFeedItemView(itemId));

  private void Dispatch(ApiRequest request) => LastDispatch = SendAsync(request);

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "A product-surface signal is dropped so a failed beacon cannot block or fail the screen.")]
  private async Task SendAsync(ApiRequest request)
  {
    try
    {
      await client.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
    }
    catch (Exception)
    {
    }
  }
}
