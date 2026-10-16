using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Support;

internal static class ClientErrorText
{
  internal static UiText For(Exception error, UiMessageKey fallback)
  {
    if (error is VouchaApiException { StatusCode: { } status, ApiMessage: { } message }
        && (int)status is >= 400 and < 500
        && !string.IsNullOrWhiteSpace(message))
    {
      return UiText.UserContent(message);
    }

    return UiText.Localized(fallback);
  }
}
