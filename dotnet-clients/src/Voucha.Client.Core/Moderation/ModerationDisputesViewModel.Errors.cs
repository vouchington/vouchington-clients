using System.Net;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed partial class ModerationDisputesViewModel
{
  private static bool IsAmbiguous(Exception exception) =>
      exception is OperationCanceledException ||
      exception is HttpRequestException http &&
      (http.StatusCode is null ||
          http.StatusCode == HttpStatusCode.RequestTimeout ||
          http.StatusCode >= HttpStatusCode.InternalServerError);

  private static UiMessageKey PresentationErrorFor(Exception exception) =>
      exception switch
      {
        TimeoutException => UiMessageKey.NativeSwiftReviewDisputesRerunStillProcessing,
        InvalidOperationException => UiMessageKey.NativeSwiftReviewDisputesRerunNotQueued,
        _ => UiMessageKey.NativeSwiftModerationReportsActionFailed,
      };
}
