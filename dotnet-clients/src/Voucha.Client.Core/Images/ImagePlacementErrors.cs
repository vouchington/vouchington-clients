using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Images;

public static class ImagePlacementErrors
{
  public static string Message(Exception exception, IUiLocalization localization) =>
      exception is VouchaApiException { ErrorCode: "IMAGE_PLACEMENT_LIMIT_REACHED" }
          ? localization.Localize(UiMessageKey.NativeCommonImagePlacementLimitReached)
          : exception.Message;
}
