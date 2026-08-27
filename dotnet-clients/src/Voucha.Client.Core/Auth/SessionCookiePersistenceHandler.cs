namespace Voucha.Client.Core.Auth;

public sealed class SessionCookiePersistenceHandler : DelegatingHandler
{
  private readonly SessionCookieJar cookieJar;

  public SessionCookiePersistenceHandler(SessionCookieJar cookieJar, HttpMessageHandler innerHandler)
      : base(innerHandler) =>
      this.cookieJar = cookieJar ?? throw new ArgumentNullException(nameof(cookieJar));

  protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    await cookieJar.RestoreAsync(cancellationToken).ConfigureAwait(false);
    var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
    {
      cookieJar.StoreSetCookieHeaders(request.RequestUri, setCookieHeaders);
      try
      {
        using var persistTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await cookieJar.PersistAsync(persistTimeout.Token).ConfigureAwait(false);
      }
      catch (TaskCanceledException)
      {
        // A persistence timeout should not hide the response that carried the cookies.
      }
    }

    return response;
  }
}
