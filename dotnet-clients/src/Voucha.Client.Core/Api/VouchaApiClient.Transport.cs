using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public async Task<T> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    using var message = new HttpRequestMessage(request.Method, BuildUri(request));
    ApplyHeaders(message, request);
    if (request.Body is not null)
    {
      message.Content = JsonContent.Create(request.Body, options: VouchaApiJson.Options);
    }

    using var response = await httpClient
        .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
        .ConfigureAwait(false);
    if (!response.IsSuccessStatusCode)
    {
      var responseBody = response.Content is not null
          ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
          : null;
      throw new VouchaApiException(
          response.StatusCode,
          string.IsNullOrEmpty(responseBody) ? null : responseBody);
    }

    if (response.Content is null) throw DecodeFailure(response, "empty response body");

    if (response.Content.Headers.ContentLength == 0)
    {
      throw DecodeFailure(response, "empty response body");
    }

    using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    if (responseStream.CanSeek && responseStream.Length == 0)
    {
      throw DecodeFailure(response, "empty response body");
    }

    try
    {
      var body = await JsonSerializer.DeserializeAsync<T>(
          responseStream, VouchaApiJson.Options, cancellationToken).ConfigureAwait(false);
      return body ?? throw DecodeFailure(response, "JSON null response body");
    }
    catch (JsonException exception)
    {
      throw DecodeFailure(response, "invalid JSON response body", exception);
    }
  }

  public async Task SendAsync(ApiRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    using var message = new HttpRequestMessage(request.Method, BuildUri(request));
    ApplyHeaders(message, request);
    if (request.Body is not null)
    {
      message.Content = JsonContent.Create(request.Body, options: VouchaApiJson.Options);
    }

    using var response = await httpClient
        .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
        .ConfigureAwait(false);
    if (!response.IsSuccessStatusCode)
    {
      var responseBody = response.Content is not null
          ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
          : null;
      throw new VouchaApiException(
          response.StatusCode,
          string.IsNullOrEmpty(responseBody) ? null : responseBody);
    }
  }

  private static string BuildUri(ApiRequest request)
  {
    if (request.Query.Count == 0) return request.Path;

    var builder = new StringBuilder(request.Path);
    builder.Append('?');
    var separator = string.Empty;
    foreach (var (key, value) in request.Query.OrderBy(entry => entry.Key, StringComparer.Ordinal))
    {
      builder.Append(separator);
      builder.Append(Uri.EscapeDataString(key));
      builder.Append('=');
      builder.Append(Uri.EscapeDataString(value));
      separator = "&";
    }

    return builder.ToString();
  }

  private static void ApplyHeaders(HttpRequestMessage message, ApiRequest request)
  {
    foreach (var (name, value) in request.Headers)
    {
      message.Headers.TryAddWithoutValidation(name, value);
    }
  }

  private static VouchaApiDecodeException DecodeFailure(
      HttpResponseMessage response, string reason, Exception? inner = null) =>
      new(response.StatusCode,
          $"Voucha API returned {(reason.StartsWith("JSON", StringComparison.Ordinal) ? "a" : "an")} {reason}.",
          inner);

  private static T Require<T>(T? value)
      where T : class =>
      value ?? throw new ArgumentNullException(nameof(value));
}
