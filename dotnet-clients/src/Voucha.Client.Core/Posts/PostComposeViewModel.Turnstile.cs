using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Posts;

public sealed partial class PostComposeViewModel
{
  public void ReportTurnstileChallengeFailure(string message)
  {
    TurnstileToken = "";
    CompleteError(string.IsNullOrWhiteSpace(message)
        ? localization.Localize(UiMessageKey.NativeDotnetCsharpTurnstileChallengeFailed)
        : message);
  }
}
