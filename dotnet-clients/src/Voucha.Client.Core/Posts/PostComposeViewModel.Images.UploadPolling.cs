using System.Net;
using System.Threading;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  private async Task<ImageUploadState> WaitForImageReadyAsync(
      string imageId,
      int imageDraftGeneration,
      CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < imageUploadPollAttempts; attempt++)
    {
      if (!IsCurrentImageDraftGeneration(imageDraftGeneration))
      {
        throw new OperationCanceledException(cancellationToken);
      }

      ImageUploadState? state = null;
      try
      {
        var response = await imageUploadService!.FetchUploadStateAsync(imageId, cancellationToken).ConfigureAwait(true);
        state = response.UploadState;
        if (state.Ready || state.Blocked || IsTerminalImageUploadState(state.UploadStatus))
        {
          return state;
        }
      }
      catch (Exception ex) when (IsTransientImageUploadFailure(ex))
      {
        if (attempt + 1 >= imageUploadPollAttempts)
        {
          throw;
        }
      }

      UpdateImage(imageId, image => image with { UploadProgress = Math.Min(0.95d, (attempt + 1d) / imageUploadPollAttempts) });

      if (attempt + 1 < imageUploadPollAttempts)
      {
        await DelayForImageUploadPollAsync(cancellationToken).ConfigureAwait(true);
      }
      else
      {
        break;
      }
    }

    throw new TimeoutException(localization.Localize(UiMessageKey.NativeDotnetCsharpImageUploadTimedOut));
  }

  private async Task DelayForImageUploadPollAsync(CancellationToken cancellationToken)
  {
    if (imageUploadPollInterval > TimeSpan.Zero)
    {
      await Task.Delay(imageUploadPollInterval, cancellationToken).ConfigureAwait(true);
      return;
    }

#pragma warning disable CA2007
    await Task.Yield();
#pragma warning restore CA2007
  }

  private static bool IsTransientImageUploadFailure(Exception exception) =>
      exception switch
      {
        OperationCanceledException => false,
        VouchaApiException apiException when apiException.StatusCode is HttpStatusCode statusCode =>
            IsTransientHttpStatusCode(statusCode),
        HttpRequestException requestException when requestException.StatusCode is null => true,
        HttpRequestException requestException when requestException.StatusCode is HttpStatusCode statusCode =>
            IsTransientHttpStatusCode(statusCode),
        _ => false,
      };

  private static bool IsTransientHttpStatusCode(HttpStatusCode statusCode)
  {
    var status = (int)statusCode;
    return status == 429 || status is >= 500 and <= 599;
  }

  private static bool IsTerminalImageUploadState(string uploadStatus) =>
      string.Equals(uploadStatus, "complete", StringComparison.Ordinal) ||
      string.Equals(uploadStatus, "failed", StringComparison.Ordinal);
}
