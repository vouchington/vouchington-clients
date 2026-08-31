using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  private const int MaximumErrorBodyBytes = 64 * 1024;

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
      var responseBody = await ReadErrorBodyAsync(response.Content, cancellationToken).ConfigureAwait(false);
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
      var responseBody = await ReadErrorBodyAsync(response.Content, cancellationToken).ConfigureAwait(false);
      throw new VouchaApiException(
          response.StatusCode,
          string.IsNullOrEmpty(responseBody) ? null : responseBody);
    }
  }

  public async Task<string> DownloadToTemporaryFileAsync(
      ApiRequest request,
      string fileName,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

    using var message = new HttpRequestMessage(request.Method, BuildUri(request));
    ApplyHeaders(message, request);
    using var response = await httpClient
        .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
        .ConfigureAwait(false);
    if (!response.IsSuccessStatusCode)
    {
      var responseBody = await ReadErrorBodyAsync(response.Content, cancellationToken).ConfigureAwait(false);
      throw new VouchaApiException(
          response.StatusCode,
          string.IsNullOrEmpty(responseBody) ? null : responseBody);
    }
    if (response.Content is null) throw DecodeFailure(response, "empty response body");

    var directory = Path.Combine(Path.GetTempPath(), "voucha-import-export");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, $"{Guid.NewGuid():N}-{fileName}");
    try
    {
      var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
      await using (source.ConfigureAwait(false))
      {
        var destination = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);
        await using (destination.ConfigureAwait(false))
        {
          await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
          return path;
        }
      }
    }
    catch
    {
      File.Delete(path);
      throw;
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

  private static async Task<string?> ReadErrorBodyAsync(
      HttpContent? content,
      CancellationToken cancellationToken)
  {
    if (content is null || content.Headers.ContentLength > MaximumErrorBodyBytes) return null;

    var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    await using (source.ConfigureAwait(false))
    {
      using var destination = new MemoryStream(MaximumErrorBodyBytes);
      var buffer = new byte[8 * 1024];
      while (true)
      {
        var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        if (read == 0) break;
        if (destination.Length + read > MaximumErrorBodyBytes) return null;
        await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
      }
      return Encoding.UTF8.GetString(destination.GetBuffer(), 0, checked((int)destination.Length));
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
