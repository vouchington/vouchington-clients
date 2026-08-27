using Voucha.Client.Core.Communities;

namespace Voucha.Client.App.Pages;

public sealed partial class CommunityActionPage
{
  private async Task<bool> EnsureTurnstileTokenAsync()
  {
    if (viewModel.Kind != CommunityActionKind.Create)
    {
      return true;
    }

    if (viewModel.CanUseCaptchaBypass || !string.IsNullOrWhiteSpace(viewModel.TurnstileToken))
    {
      return true;
    }

    if (viewModel.Validation?.RequiresCaptcha != true)
    {
      return true;
    }

    try
    {
      viewModel.TurnstileToken = await turnstileTokenProvider.GetTokenAsync().ConfigureAwait(true);
      return true;
    }
    catch (OperationCanceledException)
    {
      return false;
    }
    catch (InvalidOperationException ex)
    {
      viewModel.ReportTurnstileChallengeFailure(ex.Message);
      return false;
    }
  }
}
