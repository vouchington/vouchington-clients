using Voucha.Client.App.Controls;
using Voucha.Client.Core.Crm;

namespace Voucha.Client.App.Pages;

public sealed partial class CrmContactPage
{
  private readonly HybridPaginationControl emailPaginationControl = new() { PaginationId = "crm-emails" };
  private readonly HybridPaginationControl notePaginationControl = new() { PaginationId = "crm-notes" };

  private void ConfigurePagination()
  {
    ConfigureControl(
        emailPaginationControl,
        nameof(CrmContactDetailViewModel.HasMoreEmails),
        nameof(CrmContactDetailViewModel.IsLoadingEmailPage),
        nameof(CrmContactDetailViewModel.HasEmailPaginationError),
        emailsView,
        OnLoadMoreEmailsRequested);
    ConfigureControl(
        notePaginationControl,
        nameof(CrmContactDetailViewModel.HasMoreNotes),
        nameof(CrmContactDetailViewModel.IsLoadingNotePage),
        nameof(CrmContactDetailViewModel.HasNotePaginationError),
        notesView,
        OnLoadMoreNotesRequested);
  }

  private static void ConfigureControl(
      HybridPaginationControl control,
      string hasMoreProperty,
      string isLoadingProperty,
      string hasErrorProperty,
      CollectionView list,
      EventHandler loadHandler)
  {
    control.SetBinding(HybridPaginationControl.HasMoreProperty, hasMoreProperty);
    control.SetBinding(HybridPaginationControl.IsLoadingProperty, isLoadingProperty);
    control.SetBinding(HybridPaginationControl.HasErrorProperty, hasErrorProperty);
    control.LoadNextPageRequested += loadHandler;
    list.Footer = control;
    list.RemainingItemsThreshold = 2;
    list.RemainingItemsThresholdReached += (_, _) => control.TryLoadAutomatically();
  }

  private async void OnLoadMoreEmailsRequested(object? sender, EventArgs args)
  {
    await viewModel.LoadMoreEmailsAsync().ConfigureAwait(true);
    ApplyFields();
  }

  private async void OnLoadMoreNotesRequested(object? sender, EventArgs args)
  {
    await viewModel.LoadMoreNotesAsync().ConfigureAwait(true);
    ApplyFields();
  }
}
