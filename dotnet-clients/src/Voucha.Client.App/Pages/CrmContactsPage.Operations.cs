using Voucha.Client.Core.Crm;

namespace Voucha.Client.App.Pages;

public sealed partial class CrmContactsPage
{
  private async void OnSearchClicked(object? sender, EventArgs e)
  {
    viewModel.SearchQuery = searchEntry.Text;
    viewModel.StatusFilter = PickerValue(statusPicker);
    viewModel.VerticalFilter = PickerValue(verticalPicker);
    viewModel.LinkedFilter = linkedPicker.SelectedIndex switch
    {
      1 => true,
      2 => false,
      _ => null,
    };
    await viewModel.LoadAsync().ConfigureAwait(true);
    contactsView.ItemsSource = viewModel.Contacts;
    errorLabel.Text = viewModel.ErrorMessage;
  }

  private async void OnCreateClicked(object? sender, EventArgs e)
  {
    viewModel.CreateName = nameEntry.Text ?? string.Empty;
    viewModel.CreateEmail = emailEntry.Text ?? string.Empty;
    viewModel.CreatePhone = phoneEntry.Text;
    viewModel.CreateVertical = PickerValue(createVerticalPicker);
    viewModel.CreateContactType = PickerValue(createTypePicker);
    viewModel.CreateFollowerCount = createFollowerCountEntry.Text;
    viewModel.CreateNotes = createNotesEditor.Text;
    await viewModel.CreateAsync().ConfigureAwait(true);
    contactsView.ItemsSource = viewModel.Contacts;
    errorLabel.Text = viewModel.ErrorMessage;
    if (viewModel.ErrorMessage is null)
    {
      ClearCreateForm();
    }
  }

  private async void OnImportClicked(object? sender, EventArgs e)
  {
    viewModel.ImportCsv = importCsvEditor.Text ?? string.Empty;
    await viewModel.ImportAsync().ConfigureAwait(true);
    contactsView.ItemsSource = viewModel.Contacts;
    errorLabel.Text = viewModel.ErrorMessage;
    if (viewModel.ErrorMessage is null)
    {
      ClearImportCsv();
    }
  }

  private async void OnOpenClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: CrmContactRow row }) return;
    var page = serviceProvider.GetRequiredService<CrmContactPage>();
    page.SetContext(row.Id);
    await Navigation.PushAsync(page).ConfigureAwait(true);
  }

  private void ClearCreateForm()
  {
    viewModel.CreateName = string.Empty;
    viewModel.CreateEmail = string.Empty;
    viewModel.CreatePhone = string.Empty;
    viewModel.CreateVertical = string.Empty;
    viewModel.CreateContactType = string.Empty;
    viewModel.CreateFollowerCount = string.Empty;
    viewModel.CreateNotes = string.Empty;
    nameEntry.Text = string.Empty;
    emailEntry.Text = string.Empty;
    phoneEntry.Text = string.Empty;
    createVerticalPicker.SelectedIndex = 0;
    createTypePicker.SelectedIndex = 0;
    createFollowerCountEntry.Text = string.Empty;
    createNotesEditor.Text = string.Empty;
  }

  private void ClearImportCsv()
  {
    viewModel.ImportCsv = string.Empty;
    importCsvEditor.Text = string.Empty;
  }
}
