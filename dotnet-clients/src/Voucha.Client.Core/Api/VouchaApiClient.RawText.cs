namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public async Task<string> SendTextAsync(
      ApiRequest request,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    using var message = new HttpRequestMessage(request.Method, BuildUri(request));
    ApplyHeaders(message, request);
    using var response = await httpClient.SendAsync(
        message,
        HttpCompletionOption.ResponseHeadersRead,
        cancellationToken).ConfigureAwait(false);
    var body = response.Content is null
        ? string.Empty
        : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    if (!response.IsSuccessStatusCode)
    {
      throw new VouchaApiException(response.StatusCode, string.IsNullOrEmpty(body) ? null : body);
    }

    return body;
  }
}
