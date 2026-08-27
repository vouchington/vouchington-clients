using System.Net;
using System.Net.Http;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;

namespace Voucha.Client.Core.Tests.Posts;

internal sealed class RecordingImageUploadService : IImageUploadService
{
  public string? ContentType { get; private set; }

  public long? ContentLength { get; private set; }

  public int CreateUploadUrlCount { get; private set; }

  public int UploadCount { get; private set; }

  public bool FailUpload { get; init; }

  public bool FailCreateUploadUrl { get; init; }

  public bool BlockCreateUploadUrl { get; init; }

  public int CompleteTransientFailuresBeforeSuccess { get; init; }

  public int FetchTransientFailuresBeforeSuccess { get; init; }

  public TaskCompletionSource<bool> CreateUploadUrlStarted { get; } =
      new(TaskCreationOptions.RunContinuationsAsynchronously);

  public TaskCompletionSource<bool> ReleaseCreateUploadUrl { get; } =
      new(TaskCreationOptions.RunContinuationsAsynchronously);

  public string CompletedImageId { get; init; } = "image-1";

  public IReadOnlyList<ImageUploadState> StateResponses { get; init; } =
      [new ImageUploadState("image-1", "complete", null, true, false)];

  public int FetchStateCount { get; private set; }

  public int CompleteCount { get; private set; }

  public Task<ImageUploadUrlResponse> CreateUploadUrlAsync(
      CreateImageUploadUrlBody body,
      CancellationToken cancellationToken = default)
  {
    CreateUploadUrlCount++;
    CreateUploadUrlStarted.TrySetResult(true);
    if (BlockCreateUploadUrl)
    {
      return WaitForCreateUploadUrlReleaseAsync(body, cancellationToken);
    }
    if (FailCreateUploadUrl)
    {
      return Task.FromException<ImageUploadUrlResponse>(new HttpRequestException("Upload URL failed."));
    }

    ContentType = body.ContentType;
    ContentLength = body.ContentLength;
    return Task.FromResult(new ImageUploadUrlResponse(new ImageUploadUrl(
        "image-1",
        "https://upload.example.test/image-1",
        body.ContentType,
        DateTimeOffset.UtcNow)));
  }

  public Task UploadAsync(
      ImageUploadUrl upload,
      Stream content,
      long contentLength,
      CancellationToken cancellationToken = default)
  {
    UploadCount++;
    return FailUpload
        ? Task.FromException(new HttpRequestException("Upload failed."))
        : Task.CompletedTask;
  }

  public Task<CompleteImageUploadResponse> CompleteAsync(
      string imageId,
      CancellationToken cancellationToken = default) =>
      ++CompleteCount <= CompleteTransientFailuresBeforeSuccess
          ? Task.FromException<CompleteImageUploadResponse>(
              new HttpRequestException("Complete failed.", null, HttpStatusCode.TooManyRequests))
          : Task.FromResult(new CompleteImageUploadResponse(new ImageUpload(CompletedImageId, "complete")));

  public Task<ImageUploadStateResponse> FetchUploadStateAsync(
      string imageId,
      CancellationToken cancellationToken = default)
  {
    FetchStateCount++;
    if (FetchStateCount <= FetchTransientFailuresBeforeSuccess)
    {
      return Task.FromException<ImageUploadStateResponse>(
          new VouchaApiException(HttpStatusCode.ServiceUnavailable, null));
    }

    var index = Math.Min(FetchStateCount - FetchTransientFailuresBeforeSuccess - 1, StateResponses.Count - 1);
    return Task.FromResult(new ImageUploadStateResponse(StateResponses[index]));
  }

  private async Task<ImageUploadUrlResponse> WaitForCreateUploadUrlReleaseAsync(
      CreateImageUploadUrlBody body,
      CancellationToken cancellationToken)
  {
    await ReleaseCreateUploadUrl.Task.WaitAsync(cancellationToken);
    if (FailCreateUploadUrl)
    {
      throw new HttpRequestException("Upload URL failed.");
    }

    ContentType = body.ContentType;
    ContentLength = body.ContentLength;
    return new ImageUploadUrlResponse(new ImageUploadUrl(
        "image-1",
        "https://upload.example.test/image-1",
        body.ContentType,
        DateTimeOffset.UtcNow));
  }
}
