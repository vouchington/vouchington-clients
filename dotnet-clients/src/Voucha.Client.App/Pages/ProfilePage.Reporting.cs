using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.App;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class ProfilePage
{
  private async void OnReportUserClicked(object? sender, EventArgs e)
  {
    await NativeReportPrompt.ShowAsync(
        this,
        UiText.Localized(UiMessageKey.NativeDotnetCsharpDialogsReportUserQuestion),
        serviceProvider.GetRequiredService<ITurnstileTokenProvider>(),
        async (reason, note, token) =>
        {
          var submitted = await viewModel.ReportUserAsync(reason, note, token).ConfigureAwait(true);
          return submitted
              ? NativeReportSubmissionResult.Success
              : NativeReportSubmissionResult.Failure(
                  string.IsNullOrWhiteSpace(viewModel.ErrorMessage)
                      ? UiText.Localized(UiMessageKey.NativeSwiftModerationReportsActionFailed)
                      : UiText.ExternalContent(viewModel.ErrorMessage));
        });
  }
}
