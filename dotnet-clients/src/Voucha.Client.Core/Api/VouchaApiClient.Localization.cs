using System.Net;
using System.Text.Json;

namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public async Task<LocalizationBatchResponse?> FetchLocalizationAsync(
      string consumer,
      string locales,
      string selectors,
      string? etag = null,
      CancellationToken cancellationToken = default)
  {
    var request = VouchaApiEndpoints.Localization(consumer, locales, selectors, etag);
    using var message = new HttpRequestMessage(request.Method, BuildUri(request));
    ApplyHeaders(message, request);
    using var response = await httpClient
        .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
        .ConfigureAwait(false);
    if (response.StatusCode == HttpStatusCode.NotModified) return null;
    if (!response.IsSuccessStatusCode)
    {
      var responseBody = await ReadErrorBodyAsync(response.Content, cancellationToken)
          .ConfigureAwait(false);
      throw new VouchaApiException(
          response.StatusCode,
          string.IsNullOrEmpty(responseBody) ? null : responseBody);
    }
    if (response.Content is null) throw DecodeFailure(response, "empty response body");
    using var responseStream = await response.Content
        .ReadAsStreamAsync(cancellationToken)
        .ConfigureAwait(false);
    try
    {
      var body = await JsonSerializer.DeserializeAsync<LocalizationBatchResponse>(
          responseStream, VouchaApiJson.Options, cancellationToken).ConfigureAwait(false);
      return body ?? throw DecodeFailure(response, "JSON null response body");
    }
    catch (JsonException exception)
    {
      throw DecodeFailure(response, "invalid JSON response body", exception);
    }
  }
}
