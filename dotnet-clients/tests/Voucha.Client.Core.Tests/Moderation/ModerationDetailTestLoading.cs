using Voucha.Client.Core.Moderation;

namespace Voucha.Client.Core.Tests.Moderation;

internal static class ModerationDetailTestLoading
{
  public static async Task LoadWithDetailAsync(this ModerationDisputesViewModel viewModel,
      CancellationToken cancellationToken = default)
  {
    await viewModel.LoadAsync(cancellationToken);
    if (viewModel.CanAccess && viewModel.Summaries.Count == 1)
      await viewModel.FetchDetailAsync(viewModel.Summaries[0].Id, cancellationToken);
  }

  public static async Task LoadWithDetailAsync(this ModerationAppealsViewModel viewModel,
      CancellationToken cancellationToken = default)
  {
    await viewModel.LoadAsync(cancellationToken);
    if (viewModel.CanAccess && viewModel.Summaries.Count == 1)
      await viewModel.FetchDetailAsync(viewModel.Summaries[0].Id, cancellationToken);
  }
}
