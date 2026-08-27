using Voucha.Client.App.Controls;
using Voucha.Client.Core.Crm;

namespace Voucha.Client.App.Pages;

public sealed partial class CrmContactsPage
{
  private readonly HybridPaginationControl paginationControl = new() { PaginationId = "crm-contacts" };

  private void ConfigurePagination()
  {
    paginationControl.SetBinding(
        HybridPaginationControl.HasMoreProperty, nameof(CrmContactsViewModel.HasMoreContacts));
    paginationControl.SetBinding(
        HybridPaginationControl.IsLoadingProperty, nameof(CrmContactsViewModel.IsLoadingContactPage));
    paginationControl.SetBinding(
        HybridPaginationControl.HasErrorProperty, nameof(CrmContactsViewModel.HasContactPaginationError));
    paginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
    contactsView.Footer = paginationControl;
    contactsView.RemainingItemsThreshold = 2;
    contactsView.RemainingItemsThresholdReached += (_, _) =>
        paginationControl.TryLoadAutomatically();
  }

  private async void OnLoadNextPageRequested(object? sender, EventArgs args)
  {
    await viewModel.LoadMoreContactsAsync().ConfigureAwait(true);
    contactsView.ItemsSource = viewModel.Contacts;
  }
}
