namespace Voucha.Client.Core.Auth;

public sealed class SessionCookiePersistenceHandler : DelegatingHandler
{
  private readonly SessionCookieJar cookieJar;

  public SessionCookiePersistenceHandler(SessionCookieJar cookieJar, HttpMessageHandler innerHandler)
      : base(innerHandler)
  {
    this.cookieJar = cookieJar ?? throw new ArgumentNullException(nameof(cookieJar));
    var handler = innerHandler;
    while (handler is DelegatingHandler delegating) handler = delegating.InnerHandler!;
    if (handler is SocketsHttpHandler sockets)
    {
      sockets.UseCookies = false;
      sockets.AllowAutoRedirect = false;
    }
    if (handler is HttpClientHandler http)
    {
      http.UseCookies = false;
      http.AllowAutoRedirect = false;
    }
  }

  protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var uri = request.RequestUri ?? throw new InvalidOperationException("Request URI is unavailable.");
    var sessionRequest = request.Method == HttpMethod.Patch && uri.AbsolutePath == "/api/v1/session" &&
        request.Options.TryGetValue(Voucha.Client.Core.Api.ClientMetadataHandler.CoordinatedRotation, out var coordinated) && coordinated;
    var snapshot = await cookieJar.PrepareRequestAsync(uri, sessionRequest, cancellationToken).ConfigureAwait(false);
    var logout = uri.AbsolutePath == "/api/v1/auth/logout" && cookieJar.IsFirstParty(uri);
    var cookies = logout && request.Headers.TryGetValues("Cookie", out var captured)
        ? string.Join("; ", captured) : snapshot.Header;
    request.Headers.Remove("Cookie");
    if (!string.IsNullOrEmpty(cookies)) request.Headers.TryAddWithoutValidation("Cookie", cookies);
    request.Options.Set(Voucha.Client.Core.Api.ClientMetadataHandler.SessionDispatch, (snapshot.Generation, snapshot.Revision));
    var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    if (request.RequestUri?.AbsolutePath != "/api/v1/auth/logout" &&
        !sessionRequest &&
        response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
    {
      try
      {
        using var persistTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var authenticationCommit = response.IsSuccessStatusCode &&
            request.Options.TryGetValue(Voucha.Client.Core.Api.ClientMetadataHandler.AuthenticationTransition, out var transition) &&
            transition && setCookieHeaders.Any(header =>
                header.TrimStart().StartsWith("dt=", StringComparison.Ordinal) || header.TrimStart().StartsWith("st=", StringComparison.Ordinal));
        await cookieJar.ApplyResponseCookiesAsync(
            request.RequestUri, setCookieHeaders, snapshot.Generation, snapshot.Revision, authenticationCommit, persistTimeout.Token).ConfigureAwait(false);
      }
      catch (TaskCanceledException)
      {
        // A persistence timeout should not hide the response that carried the cookies.
      }
    }

    return response;
  }
}
