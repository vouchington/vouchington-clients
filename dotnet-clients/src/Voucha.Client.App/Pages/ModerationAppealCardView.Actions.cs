using Voucha.Client.Core.Api;

namespace Voucha.Client.App.Pages;

internal sealed partial class ModerationAppealCardView
{
  private void ConfigureActions()
  {
    save.Clicked += async (_, _) => await WithAppeal(async (appeal, token) =>
    {
      _ = await viewModel.SaveDraftAsync(appeal, token).ConfigureAwait(true);
    }).ConfigureAwait(true);
    approve.Clicked += async (_, _) => await WithAppeal(viewModel.ApproveAsync).ConfigureAwait(true);
    deliver.Clicked += async (_, _) => await WithAppeal(viewModel.DeliverAsync).ConfigureAwait(true);
    rerun.Clicked += async (_, _) => await WithAppeal(viewModel.RerunAsync).ConfigureAwait(true);
    refreshAIRerun.Clicked += async (_, _) => await WithAppeal(viewModel.RefreshPendingAIRerunAsync).ConfigureAwait(true);
    accept.Clicked += async (_, _) => await ResolveAsync(ModerationAppealAction.Accept).ConfigureAwait(true);
    reduce.Clicked += async (_, _) => await ResolveAsync(ModerationAppealAction.Reduce).ConfigureAwait(true);
    deny.Clicked += async (_, _) => await ResolveAsync(ModerationAppealAction.Deny).ConfigureAwait(true);
    refreshDelivery.Clicked += async (_, _) => await WithAppeal(viewModel.RefreshDeliveryStatusAsync).ConfigureAwait(true);
  }

  private Task WithAppeal(Func<ModerationAppeal, CancellationToken, Task> action) =>
      Appeal is { } appeal
          ? RunLifecycleActionAsync(token => action(appeal, token))
          : Task.CompletedTask;

  private Task ResolveAsync(ModerationAppealAction action) =>
      Appeal is { } appeal
          ? RunLifecycleActionAsync(token => viewModel.ResolveAsync(appeal, action, token))
          : Task.CompletedTask;

  private async Task RunLifecycleActionAsync(Func<CancellationToken, Task> action)
  {
    var token = lifecycleToken();
    try
    {
      await action(token).ConfigureAwait(true);
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested)
    {
    }
  }
}
