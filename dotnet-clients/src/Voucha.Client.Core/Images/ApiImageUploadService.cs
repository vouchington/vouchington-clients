using System.Net.Http.Headers;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Images;

public sealed class ApiImageUploadService : IImageUploadService
{
  private readonly VouchaApiClient client;
  private readonly HttpClient httpClient;

  public ApiImageUploadService(VouchaApiClient client, HttpClient httpClient)
  {
    this.client = client ?? throw new ArgumentNullException(nameof(client));
    this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
  }

  public Task<ImageUploadUrlResponse> CreateUploadUrlAsync(
      CreateImageUploadUrlBody body,
      CancellationToken cancellationToken = default) =>
      client.CreateImageUploadUrlAsync(body, cancellationToken);

  public async Task UploadAsync(
      ImageUploadUrl upload,
      Stream content,
      long contentLength,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(upload);
    ArgumentNullException.ThrowIfNull(content);
    using var request = new HttpRequestMessage(HttpMethod.Put, ValidateUploadUrl(upload.UploadUrl));
    request.Content = new StreamContent(content);
    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(upload.ContentType);
    request.Content.Headers.ContentLength = contentLength;
    using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
  }

  public Task<CompleteImageUploadResponse> CompleteAsync(
      string imageId,
      CancellationToken cancellationToken = default) =>
      client.CompleteImageUploadAsync(imageId, cancellationToken);

  public Task<ImageUploadStateResponse> FetchUploadStateAsync(
      string imageId,
      CancellationToken cancellationToken = default) =>
      client.FetchImageUploadStateAsync(imageId, cancellationToken);

  private static Uri ValidateUploadUrl(string uploadUrl)
  {
    if (Uri.TryCreate(uploadUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps ||
         (uri.Scheme == Uri.UriSchemeHttp && IsLocalhost(uri.Host))))
    {
      return uri;
    }

    throw new InvalidOperationException("Invalid image upload URL.");
  }

  private static bool IsLocalhost(string host) =>
      string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
      host == "127.0.0.1" ||
      host == "::1";
}
