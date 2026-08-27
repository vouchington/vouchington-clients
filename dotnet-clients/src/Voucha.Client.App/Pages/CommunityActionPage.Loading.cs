using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Communities;

namespace Voucha.Client.App.Pages;

public sealed partial class CommunityActionPage
{
  public CommunityActionKind Kind => viewModel.Kind;

  public void SetContext(string? communitySlug = null, string? inviteCode = null)
  {
    if (!string.IsNullOrWhiteSpace(communitySlug))
    {
      communitySlugEntry.Text = communitySlug;
      viewModel.CommunitySlug = communitySlug;
    }

    if (!string.IsNullOrWhiteSpace(inviteCode))
    {
      codeEntry.Text = inviteCode;
      viewModel.Code = inviteCode;
    }

    Render();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await LoadApplicationQuestionsAsync().ConfigureAwait(true);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native page load failures are displayed in page state.")]
  private async Task LoadApplicationQuestionsAsync()
  {
    try
    {
      viewModel.CommunitySlug = communitySlugEntry.Text ?? string.Empty;
      await viewModel.LoadApplicationQuestionsAsync().ConfigureAwait(true);
      Render();
    }
    catch (Exception ex)
    {
      errorLabel.Text = ex.Message;
      errorLabel.IsVisible = true;
    }
  }
}
