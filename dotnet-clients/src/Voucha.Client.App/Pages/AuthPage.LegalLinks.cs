namespace Voucha.Client.App.Pages;

public partial class AuthPage
{
  private async void OnPrivacyPolicyClicked(object? sender, EventArgs e) =>
      await OpenNativePathAsync("/article/privacy-policy");

  private async void OnTermsOfServiceClicked(object? sender, EventArgs e) =>
      await OpenNativePathAsync("/article/terms-of-service");

  private async void OnCommunityGuidelinesClicked(object? sender, EventArgs e) =>
      await OpenNativePathAsync("/article/community-guidelines");

  private static Task OpenNativePathAsync(string targetPath) =>
      Shell.Current is AppShell appShell
          ? appShell.OpenNativePathAsync(targetPath)
          : Task.CompletedTask;
}
