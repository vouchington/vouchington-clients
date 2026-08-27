using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Images;

public interface IImageUploadService
{
  Task<ImageUploadUrlResponse> CreateUploadUrlAsync(
      CreateImageUploadUrlBody body,
      CancellationToken cancellationToken = default);

  Task UploadAsync(
      ImageUploadUrl upload,
      Stream content,
      long contentLength,
      CancellationToken cancellationToken = default);

  Task<CompleteImageUploadResponse> CompleteAsync(
      string imageId,
      CancellationToken cancellationToken = default);

  Task<ImageUploadStateResponse> FetchUploadStateAsync(
      string imageId,
      CancellationToken cancellationToken = default);
}
