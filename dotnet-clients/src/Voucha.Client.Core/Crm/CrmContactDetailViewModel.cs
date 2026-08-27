using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using System.Globalization;
using System.Threading;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Crm;

public sealed partial class CrmContactDetailViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly ICrmService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private int loadGeneration;
  private string? contactId;
  private CrmContact? contact;
  private IReadOnlyList<CrmContactSocialAccount> socialAccounts = [];
  private IReadOnlyList<CrmEmailRow> emails = [];
  private IReadOnlyList<CrmNoteRow> notes = [];
  private string name = string.Empty;
  private string email = string.Empty;
  private string? phone;
  private string? vertical;
  private string? contactType;
  private string? followerCount;
  private string? notesText;
  private string? linkUserId;
  private string? emailSubject;
  private string? emailBodyHtml;
  private string? emailBodyText;
  private CrmEmailProvider emailProvider = CrmEmailProvider.Ses;
  private DateTimeOffset? draftGeneratedAt;
  private string? draftPrompt;
  private string? draftTone;
  private string? noteBody;
  private string? errorMessage;
  private bool isLoading;
  private bool isSaving;
  private bool isArchiving;
  private bool isLinking;
  private bool isSendingEmail;
  private bool isDrafting;
  private bool isCreatingNote;

  public CrmContactDetailViewModel(
      ICrmService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public string? ContactId { get => contactId; private set => SetProperty(ref contactId, value); }
  public CrmContact? Contact { get => contact; private set => SetProperty(ref contact, value); }
  public IReadOnlyList<CrmContactSocialAccount> SocialAccounts { get => socialAccounts; private set => SetProperty(ref socialAccounts, value); }
  public IReadOnlyList<CrmEmailRow> Emails { get => emails; private set => SetProperty(ref emails, value); }
  public IReadOnlyList<CrmNoteRow> Notes { get => notes; private set => SetProperty(ref notes, value); }
  public string Name { get => name; set => SetProperty(ref name, value); }
  public string Email { get => email; set => SetProperty(ref email, value); }
  public string? Phone { get => phone; set => SetProperty(ref phone, value); }
  public string? Vertical { get => vertical; set => SetProperty(ref vertical, value); }
  public string? ContactType { get => contactType; set => SetProperty(ref contactType, value); }
  public string? FollowerCount { get => followerCount; set => SetProperty(ref followerCount, value); }
  public string? NotesText { get => notesText; set => SetProperty(ref notesText, value); }
  public string? LinkUserId { get => linkUserId; set => SetProperty(ref linkUserId, value); }
  public string? EmailSubject { get => emailSubject; set => SetProperty(ref emailSubject, value); }
  public string? EmailBodyHtml { get => emailBodyHtml; set => SetProperty(ref emailBodyHtml, value); }
  public string? EmailBodyText { get => emailBodyText; set => SetProperty(ref emailBodyText, value); }
  public CrmEmailProvider EmailProvider { get => emailProvider; set => SetProperty(ref emailProvider, value); }
  public DateTimeOffset? DraftGeneratedAt { get => draftGeneratedAt; private set => SetProperty(ref draftGeneratedAt, value); }
  public string? DraftPrompt { get => draftPrompt; set => SetProperty(ref draftPrompt, value); }
  public string? DraftTone { get => draftTone; set => SetProperty(ref draftTone, value); }
  public string? NoteBody { get => noteBody; set => SetProperty(ref noteBody, value); }
  public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }
  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
  public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
  public bool IsSaving { get => isSaving; private set => SetProperty(ref isSaving, value); }
  public bool IsArchiving { get => isArchiving; private set => SetProperty(ref isArchiving, value); }
  public bool IsLinking { get => isLinking; private set => SetProperty(ref isLinking, value); }
  public bool IsSendingEmail { get => isSendingEmail; private set => SetProperty(ref isSendingEmail, value); }
  public bool IsDrafting { get => isDrafting; private set => SetProperty(ref isDrafting, value); }
  public bool IsCreatingNote { get => isCreatingNote; private set => SetProperty(ref isCreatingNote, value); }

  public Task LoadAsync(string contactId, CancellationToken cancellationToken = default) =>
      LoadCoreAsync(contactId, cancellationToken);

  private async Task LoadCoreAsync(string nextContactId, CancellationToken cancellationToken)
  {
    var generation = Interlocked.Increment(ref loadGeneration);
    ContactId = nextContactId;
    ResetDetailPagination();
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      var detailTask = service.FetchContactAsync(nextContactId, cancellationToken);
      var emailsTask = service.FetchContactEmailsAsync(nextContactId, cancellationToken: cancellationToken);
      var notesTask = service.FetchContactNotesAsync(nextContactId, cancellationToken: cancellationToken);
      await Task.WhenAll(detailTask, emailsTask, notesTask).ConfigureAwait(true);
      if (generation != loadGeneration || ContactId != nextContactId) return;
      var detail = await detailTask.ConfigureAwait(true);
      ApplyContact(detail.Contact);
      SocialAccounts = detail.SocialAccounts;
      ApplyInitialEmailPage(await emailsTask.ConfigureAwait(true));
      ApplyInitialNotePage(await notesTask.ConfigureAwait(true));
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (generation == loadGeneration)
      {
        ErrorMessage = ex.Message;
      }
    }
    finally
    {
      if (generation == loadGeneration)
      {
        IsLoading = false;
      }
    }
  }

  public void OnUiLocaleChanged()
  {
    Emails = Emails.Select(row => row.WithLocalization(localization)).ToArray();
    Notes = Notes.Select(row => row.WithLocalization(localization)).ToArray();
    SynchronizeDetailPages();
  }

  public void Dispose() => localeSubscription?.Dispose();

  private void ApplyContact(CrmContact nextContact)
  {
    Contact = nextContact;
    Name = nextContact.Name;
    Email = nextContact.Email;
    Phone = nextContact.Phone;
    Vertical = nextContact.Vertical.ToCrmFieldValue();
    ContactType = nextContact.ContactType.ToCrmFieldValue();
    FollowerCount = nextContact.FollowerCount?.ToString(CultureInfo.InvariantCulture);
    NotesText = nextContact.Notes;
  }
}
