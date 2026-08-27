using Voucha.Client.Core.Settings;

namespace Voucha.Client.App.Pages;

public partial class SettingsPage
{
  private async void OnSaveLocalLLMClicked(object? sender, EventArgs e)
  {
    await viewModel.SaveLocalLLMSettingsAsync();
  }

  private async void OnTestLocalLLMClicked(object? sender, EventArgs e)
  {
    await viewModel.TestLocalLLMSettingsAsync();
  }

  private void OnClearLocalLLMDraftClicked(object? sender, EventArgs e)
  {
    viewModel.ResetLocalLLMEndpointDraft();
  }

  private async void OnEditLocalLLMEndpointClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: SettingsLocalLLMEndpointRow row })
    {
      await viewModel.SelectLocalLLMEndpointDraftAsync(row.ProtocolValue);
    }
  }

  private async void OnActivateLocalLLMEndpointClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: SettingsLocalLLMEndpointRow row })
    {
      await viewModel.ActivateLocalLLMEndpointAsync(row.ProtocolValue);
    }
  }

  private async void OnDeleteLocalLLMEndpointClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: SettingsLocalLLMEndpointRow row })
    {
      await viewModel.DeleteLocalLLMEndpointAsync(row.ProtocolValue);
    }
  }
}
