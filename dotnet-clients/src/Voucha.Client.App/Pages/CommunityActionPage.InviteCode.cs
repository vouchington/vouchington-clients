using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class CommunityActionPage
{
  private readonly VerticalStackLayout inviteCodeNotice = new() { Spacing = 8 };
  private Entry? inviteCodeValue;

  private void RenderInviteCodeNotice()
  {
    inviteCodeNotice.IsVisible = !string.IsNullOrEmpty(viewModel.CreatedInviteCode);
    if (!inviteCodeNotice.IsVisible)
    {
      if (inviteCodeValue is not null) inviteCodeValue.Text = null;
      return;
    }

    if (inviteCodeValue is null)
    {
      inviteCodeNotice.Children.Add(UiCopy.Bind(
          new Label(), Label.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesOneTimeInviteCodeNotice));
      inviteCodeValue = new Entry
      {
        IsReadOnly = true,
        IsTextPredictionEnabled = false,
        IsSpellCheckEnabled = false,
      };
      inviteCodeNotice.Children.Add(inviteCodeValue);
      var dismissButton = UiCopy.Bind(
          new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesDismissInviteCode);
      dismissButton.Clicked += (_, _) => viewModel.DismissInviteCode();
      inviteCodeNotice.Children.Add(dismissButton);
    }

    inviteCodeValue.Text = UiCopy.Resolve(UiText.Verbatim(viewModel.CreatedInviteCode!));
  }
}
