using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Crm;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class CrmContactPage : ContentPage
{
  private readonly CrmContactDetailViewModel viewModel;
  private readonly Entry nameEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmName);
  private readonly Entry emailEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmEmail);
  private readonly Entry phoneEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmPhone);
  private readonly Entry verticalEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmVertical);
  private readonly Entry contactTypeEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmType);
  private readonly Entry followerCountEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmFollowerCount);
  private readonly Editor notesEditor = UiCopy.Bind(new Editor { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 96 }, Editor.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmNotes);
  private readonly Entry linkUserEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmUserId);
  private readonly Picker emailProviderPicker = new()
  {
    ItemsSource = new[] { UiExternalProviderText.AmazonSes.Value, UiExternalProviderText.GmailSmtp.Value },
    SelectedIndex = 0,
  };
  private readonly Entry emailSubjectEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmEmailSubject);
  private readonly Editor emailHtmlEditor = UiCopy.Bind(new Editor { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 100 }, Editor.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmEmailHtml);
  private readonly Editor emailTextEditor = UiCopy.Bind(new Editor { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 100 }, Editor.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmEmailText);
  private readonly Entry draftPromptEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmDraftPrompt);
  private readonly Entry draftToneEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmDraftTone);
  private readonly Editor noteBodyEditor = UiCopy.Bind(new Editor { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 96 }, Editor.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCrmNewNote);
  private readonly Label errorLabel = new() { TextColor = Colors.IndianRed };
  private readonly CollectionView socialAccountsView = new();
  private readonly CollectionView emailsView = new();
  private readonly CollectionView notesView = new();
  private string? currentContactId;

  public CrmContactPage(CrmContactDetailViewModel viewModel)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetCsharpCrmCrmContact.Value);
    socialAccountsView.ItemTemplate = new DataTemplate(BuildSocialAccountTemplate);
    emailsView.ItemTemplate = new DataTemplate(BuildEmailTemplate);
    notesView.ItemTemplate = new DataTemplate(BuildNoteTemplate);
    ConfigurePagination();
    Content = new ScrollView { Content = BuildLayout() };
  }

  public void SetContext(string contactId) => currentContactId = contactId;

  public async Task ApplyRouteAsync(string contactId)
  {
    currentContactId = contactId;
    await viewModel.LoadAsync(contactId).ConfigureAwait(true);
    ApplyFields();
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native page load failures are displayed in page state.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (currentContactId is null) return;
    try
    {
      await viewModel.LoadAsync(currentContactId).ConfigureAwait(true);
      ApplyFields();
    }
    catch (Exception ex)
    {
      errorLabel.Text = ex.Message;
    }
  }

  private void ApplyFields()
  {
    nameEntry.Text = viewModel.Name;
    emailEntry.Text = viewModel.Email;
    phoneEntry.Text = viewModel.Phone;
    verticalEntry.Text = viewModel.Vertical;
    contactTypeEntry.Text = viewModel.ContactType;
    followerCountEntry.Text = viewModel.FollowerCount;
    notesEditor.Text = viewModel.NotesText;
    emailSubjectEntry.Text = viewModel.EmailSubject;
    emailHtmlEditor.Text = viewModel.EmailBodyHtml;
    emailTextEditor.Text = viewModel.EmailBodyText;
    emailProviderPicker.SelectedIndex = EmailProviderToPickerIndex(viewModel.EmailProvider);
    draftPromptEntry.Text = viewModel.DraftPrompt;
    draftToneEntry.Text = viewModel.DraftTone;
    socialAccountsView.ItemsSource = viewModel.SocialAccounts;
    emailsView.ItemsSource = viewModel.Emails;
    notesView.ItemsSource = viewModel.Notes;
    errorLabel.Text = viewModel.ErrorMessage;
  }

  private async void OnSaveClicked(object? sender, EventArgs e)
  {
    viewModel.Name = nameEntry.Text ?? string.Empty;
    viewModel.Email = emailEntry.Text ?? string.Empty;
    viewModel.Phone = phoneEntry.Text;
    viewModel.Vertical = verticalEntry.Text;
    viewModel.ContactType = contactTypeEntry.Text;
    viewModel.FollowerCount = followerCountEntry.Text;
    viewModel.NotesText = notesEditor.Text;
    await viewModel.SaveAsync().ConfigureAwait(true);
    ApplyFields();
  }

  private async void OnArchiveClicked(object? sender, EventArgs e)
  {
    if (!await viewModel.ArchiveAsync().ConfigureAwait(true)) return;
    if (Navigation.NavigationStack.Count > 1)
    {
      await Navigation.PopAsync().ConfigureAwait(true);
    }
    else if (Shell.Current is not null)
    {
      await Shell.Current.GoToAsync("//crm").ConfigureAwait(true);
    }
  }

  private async void OnLinkClicked(object? sender, EventArgs e)
  {
    viewModel.LinkUserId = linkUserEntry.Text;
    await viewModel.LinkUserAsync().ConfigureAwait(true);
    ApplyFields();
  }

  private async void OnUnlinkClicked(object? sender, EventArgs e)
  {
    await viewModel.UnlinkUserAsync().ConfigureAwait(true);
    ApplyFields();
  }

  private async void OnDraftClicked(object? sender, EventArgs e)
  {
    viewModel.DraftPrompt = draftPromptEntry.Text;
    viewModel.DraftTone = draftToneEntry.Text;
    await viewModel.GenerateDraftAsync().ConfigureAwait(true);
    ApplyFields();
  }

  private async void OnSendEmailClicked(object? sender, EventArgs e)
  {
    viewModel.EmailSubject = emailSubjectEntry.Text;
    viewModel.EmailBodyHtml = emailHtmlEditor.Text;
    viewModel.EmailBodyText = emailTextEditor.Text;
    viewModel.EmailProvider = PickerIndexToEmailProvider(emailProviderPicker.SelectedIndex);
    if (await viewModel.SendEmailAsync().ConfigureAwait(true))
    {
      viewModel.EmailSubject = string.Empty;
      viewModel.EmailBodyHtml = string.Empty;
      viewModel.EmailBodyText = string.Empty;
      viewModel.DraftPrompt = string.Empty;
      viewModel.DraftTone = string.Empty;
    }
    ApplyFields();
  }

  private async void OnAddNoteClicked(object? sender, EventArgs e)
  {
    viewModel.NoteBody = noteBodyEditor.Text;
    if (await viewModel.CreateNoteAsync().ConfigureAwait(true))
    {
      noteBodyEditor.Text = string.Empty;
      ApplyFields();
    }
  }

  private async void OnDeleteNoteClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: CrmNoteRow note }) return;
    await viewModel.DeleteNoteAsync(note.Id).ConfigureAwait(true);
    ApplyFields();
  }

  private static int EmailProviderToPickerIndex(CrmEmailProvider provider) => provider switch
  {
    CrmEmailProvider.GmailSmtp => 1,
    _ => 0,
  };

  private static CrmEmailProvider PickerIndexToEmailProvider(int selectedIndex) => selectedIndex == 1
      ? CrmEmailProvider.GmailSmtp
      : CrmEmailProvider.Ses;
}
