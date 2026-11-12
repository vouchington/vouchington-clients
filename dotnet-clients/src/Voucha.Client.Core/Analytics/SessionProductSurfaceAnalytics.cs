using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;

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
  private readonly ISessionStore sessionStore;
  private int inFlight;

  public SessionProductSurfaceAnalytics(VouchaApiClient client, ISessionStore sessionStore)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
  }

  internal Task LastDispatch { get; private set; } = Task.CompletedTask;

  public void RecordLandingPageVisit(string landingPageId, LandingPageVisitSignal signal) =>
      Dispatch(VouchaApiEndpoints.LandingPageVisit(landingPageId, signal));

  public void RecordLandingPageClick(string landingPageId, LandingPageClickSignal signal) =>
      Dispatch(VouchaApiEndpoints.LandingPageClick(landingPageId, signal));

  public void RecordRssFeedView(string rssFeedId) =>
      Dispatch(VouchaApiEndpoints.RssFeedView(rssFeedId));

  public void RecordRssFeedItemView(string itemId) =>
      Dispatch(VouchaApiEndpoints.RssFeedItemView(itemId));

  private void Dispatch(ApiRequest request)
  {
    if (!sessionStore.Current.IsAuthenticated)
    {
      return;
    }

    if (Interlocked.Increment(ref inFlight) > 4)
    {
      Interlocked.Decrement(ref inFlight);
      return;
    }
    LastDispatch = SendAsync(request);
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "A product-surface signal is dropped so a failed beacon cannot block or fail the screen.")]
  private async Task SendAsync(ApiRequest request)
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var account = sessionStore.Current.Identity?.Id;
    void AccountChanged(object? sender, SessionChangedEventArgs args)
    {
      if (args.Snapshot.Identity?.Id != account) cancellation.Cancel();
    }
    sessionStore.SessionChanged += AccountChanged;
    try
    {
      if (account is null || sessionStore.Current.Identity?.Id != account) return;
      await client.SendAsync(request, cancellation.Token).ConfigureAwait(false);
    }
    catch (Exception)
    {
    }
    finally
    {
      sessionStore.SessionChanged -= AccountChanged;
      Interlocked.Decrement(ref inFlight);
    }
  }
}
