using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public partial class EngineeringQueuesPage : ContentPage
{
  private readonly EngineeringQueuesViewModel viewModel;

  public EngineeringQueuesPage(EngineeringQueuesViewModel viewModel)
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

  private async void OnRefreshClicked(object? sender, EventArgs e)
  {
    await viewModel.LoadAsync().ConfigureAwait(true);
  }

  private async void OnTogglePauseClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: QueueStats job }) return;
    if (job.Paused)
    {
      await viewModel.ResumeQueueAsync(job.Name).ConfigureAwait(true);
      return;
    }

    await viewModel.PauseQueueAsync(job.Name).ConfigureAwait(true);
  }

  private async void OnTriggerScheduledJobClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: ScheduledJob job })
    {
      await viewModel.TriggerScheduledJobAsync(job.Id).ConfigureAwait(true);
    }
  }

  private async void OnTriggerBackfillClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: Backfill backfill })
    {
      if (!await DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpDialogsRunBackfill),
          UiCopy.Format(UiMessageKey.NativeDotnetCsharpDialogsRunBackfillDescription, ("description", backfill.Description)),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpRun),
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCancel)).ConfigureAwait(true))
      {
        return;
      }

      await viewModel.TriggerBackfillAsync(backfill.Id).ConfigureAwait(true);
    }
  }
}
