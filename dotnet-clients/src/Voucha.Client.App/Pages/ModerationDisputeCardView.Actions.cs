using Voucha.Client.Core.Api;

namespace Voucha.Client.App.Pages;

internal sealed partial class ModerationDisputeCardView
{
  private void ConfigureActions()
  {
    save.Clicked += async (_, _) => await WithDispute(async (dispute, token) =>
    {
      _ = await viewModel.SavePublicResponseAsync(dispute, token).ConfigureAwait(true);
    }).ConfigureAwait(true);
    approve.Clicked += async (_, _) =>
        await WithDispute(viewModel.ApproveAsync).ConfigureAwait(true);
    deliver.Clicked += async (_, _) =>
        await WithDispute(viewModel.DeliverAsync).ConfigureAwait(true);
    rerun.Clicked += async (_, _) =>
        await WithDispute(viewModel.RerunAsync).ConfigureAwait(true);
    remove.Clicked += async (_, _) =>
        await ResolveAsync(ModerationDisputeResolutionAction.Remove).ConfigureAwait(true);
    annotate.Clicked += async (_, _) =>
        await ResolveAsync(ModerationDisputeResolutionAction.Annotate).ConfigureAwait(true);
    dismiss.Clicked += async (_, _) =>
        await ResolveAsync(ModerationDisputeResolutionAction.Dismiss).ConfigureAwait(true);
    refresh.Clicked += async (_, _) =>
    {
      if (Dispute is { } dispute)
        await RunLifecycleActionAsync(token =>
            viewModel.RefreshAmbiguousAsync(dispute.Id, token)).ConfigureAwait(true);
    };
  }

  private Task WithDispute(Func<ModerationDispute, CancellationToken, Task> action) =>
      Dispute is { } dispute
          ? RunLifecycleActionAsync(token => action(dispute, token))
          : Task.CompletedTask;

  private Task ResolveAsync(ModerationDisputeResolutionAction action) =>
      Dispute is { } dispute
          ? RunLifecycleActionAsync(token => viewModel.ResolveAsync(dispute, action, token))
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
