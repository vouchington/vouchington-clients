using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class EngineeringValkeyPage : ContentPage
{
  private readonly EngineeringValkeyViewModel viewModel;

  public EngineeringValkeyPage(EngineeringValkeyViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle handler surfaces failures through view state.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private async void OnRefreshClicked(object? sender, EventArgs e) => await viewModel.LoadAsync().ConfigureAwait(true);

  private async void OnRebuildClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: string filter })
    {
      if (!await DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsRebuildBloomFilter),
          UiCopy.Format(UiMessageKey.NativeDotnetCsharpDialogsRebuildBloomFilterDescription, ("filter", filter)),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsRebuild),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel)).ConfigureAwait(true))
      {
        return;
      }

      await viewModel.RebuildBloomFilterAsync(filter).ConfigureAwait(true);
    }
  }

  private async void OnClearGroupClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: CacheGroup group })
    {
      if (!await DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsClearCacheGroup),
          UiCopy.Format(UiMessageKey.NativeDotnetCsharpDialogsClearCacheGroupDescription, ("group", group.Name)),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpClear),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel)).ConfigureAwait(true))
      {
        return;
      }

      await viewModel.ClearCacheAsync(group.Name).ConfigureAwait(true);
    }
  }

  private async void OnClearAllClicked(object? sender, EventArgs e)
  {
    if (!await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsClearAllCacheGroups),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsClearAllCacheGroupsDescription),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpClear),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel)).ConfigureAwait(true))
    {
      return;
    }

    await viewModel.ClearAllAsync().ConfigureAwait(true);
  }

  private async void OnFlushClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: FlushConcernOption option }) return;

    if (option.RequiresForce)
    {
      var confirmedForce = await DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsFlushSessions),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsFlushSessionsDescription),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsFlushSessionsConfirm),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel)).ConfigureAwait(true);
      if (!confirmedForce) return;

      await viewModel.FlushValkeyAsync(option.Concern, force: true).ConfigureAwait(true);
      return;
    }

    if (!await DisplayAlertAsync(
        UiCopy.Format(UiMessageKey.NativeDotnetCsharpDialogsFlushConcern, ("label", option.Label)),
        option.Description,
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsFlush),
        UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel)).ConfigureAwait(true))
    {
      return;
    }

    await viewModel.FlushValkeyAsync(option.Concern).ConfigureAwait(true);
  }
}
