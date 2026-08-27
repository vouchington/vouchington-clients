using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using System.Globalization;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Crm;

public sealed partial class CrmContactsViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly ICrmService service;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private IReadOnlyList<CrmContactRow> contacts = [];
  private string? searchQuery;
  private string? statusFilter;
  private string? verticalFilter;
  private bool? linkedFilter;
  private string createName = string.Empty;
  private string createEmail = string.Empty;
  private string? createPhone;
  private string? createVertical;
  private string? createContactType;
  private string? createFollowerCount;
  private string? createNotes;
  private string importCsv = string.Empty;
  private string? errorMessage;
  private bool isLoading;
  private bool isCreating;
  private bool isImporting;

  public CrmContactsViewModel(
      ICrmService service,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public IReadOnlyList<CrmContactRow> Contacts { get => contacts; private set => SetProperty(ref contacts, value); }
  public string? SearchQuery { get => searchQuery; set => SetProperty(ref searchQuery, value); }
  public string? StatusFilter { get => statusFilter; set => SetProperty(ref statusFilter, value); }
  public string? VerticalFilter { get => verticalFilter; set => SetProperty(ref verticalFilter, value); }
  public bool? LinkedFilter { get => linkedFilter; set => SetProperty(ref linkedFilter, value); }
  public string CreateName { get => createName; set => SetProperty(ref createName, value); }
  public string CreateEmail { get => createEmail; set => SetProperty(ref createEmail, value); }
  public string? CreatePhone { get => createPhone; set => SetProperty(ref createPhone, value); }
  public string? CreateVertical { get => createVertical; set => SetProperty(ref createVertical, value); }
  public string? CreateContactType { get => createContactType; set => SetProperty(ref createContactType, value); }
  public string? CreateFollowerCount { get => createFollowerCount; set => SetProperty(ref createFollowerCount, value); }
  public string? CreateNotes { get => createNotes; set => SetProperty(ref createNotes, value); }
  public string ImportCsv { get => importCsv; set => SetProperty(ref importCsv, value); }
  public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }
  public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
  public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
  public bool IsCreating { get => isCreating; private set => SetProperty(ref isCreating, value); }
  public bool IsImporting { get => isImporting; private set => SetProperty(ref isImporting, value); }

  public Task LoadAsync(CancellationToken cancellationToken = default) => LoadContactsAsync(cancellationToken);

  public async Task CreateAsync(CancellationToken cancellationToken = default)
  {
    if (IsCreating) return;
    if (string.IsNullOrWhiteSpace(CreateName))
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpNameRequired);
      return;
    }
    if (string.IsNullOrWhiteSpace(CreateEmail))
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpEmailRequired);
      return;
    }
    IsCreating = true;
    ErrorMessage = null;
    try
    {
      var response = await service.CreateContactAsync(
          new CreateCrmContactBody(
              CreateName.Trim(),
              CreateEmail.Trim(),
              string.IsNullOrWhiteSpace(CreatePhone) ? null : CreatePhone.Trim(),
              ParseVertical(CreateVertical),
              ParseContactType(CreateContactType),
              ParseInt(CreateFollowerCount),
              string.IsNullOrWhiteSpace(CreateNotes) ? null : CreateNotes.Trim()),
          cancellationToken).ConfigureAwait(true);
      Contacts = [CrmContactRow.FromContact(response.Contact, localization), .. Contacts.Where(row => row.Id != response.Contact.Id)];
      SynchronizeContactPage();
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsCreating = false;
    }
  }

  public async Task ImportAsync(CancellationToken cancellationToken = default)
  {
    if (IsImporting) return;
    if (string.IsNullOrWhiteSpace(ImportCsv))
    {
      ErrorMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpCsvRequired);
      return;
    }
    IsImporting = true;
    ErrorMessage = null;
    try
    {
      var response = await service.ImportContactsAsync(ImportCsv, cancellationToken).ConfigureAwait(true);
      if (!response.Valid && response.Validation is not null)
      {
        ErrorMessage = string.Join(
            "; ",
            response.Validation.Rows.SelectMany(row =>
                row.Errors.Select(error => localization.Format(
                    UiMessageKey.NativeDotnetCrmRowError,
                    ("row", row.RowIndex + 2),
                    ("error", error)))));
        return;
      }
      if (!response.Valid)
      {
        ErrorMessage = response.Error
            ?? localization.Localize(UiMessageKey.NativeDotnetCsharpCsvImportFailed);
        return;
      }

      await LoadContactsAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsImporting = false;
    }
  }

  private async Task LoadContactsAsync(CancellationToken cancellationToken)
  {
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      await LoadContactsPageAsync(replace: true, cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsLoading = false;
    }
  }

  private static CrmContactVertical? ParseVertical(string? value) => value?.Trim().ToUpperInvariant() switch
  {
    "CREDIT_CARDS" => CrmContactVertical.CreditCards,
    "TRAVEL" => CrmContactVertical.Travel,
    "CARS" => CrmContactVertical.Cars,
    "AI" => CrmContactVertical.Ai,
    "TECHNOLOGY" => CrmContactVertical.Technology,
    "FINANCE" => CrmContactVertical.Finance,
    "LIFESTYLE" => CrmContactVertical.Lifestyle,
    "OTHER" => CrmContactVertical.Other,
    _ => null,
  };

  private static CrmContactType? ParseContactType(string? value) => value?.Trim().ToUpperInvariant() switch
  {
    "INFLUENCER" => CrmContactType.Influencer,
    "CUSTOMER" => CrmContactType.Customer,
    "PARTNER" => CrmContactType.Partner,
    _ => null,
  };

  private static int? ParseInt(string? value) => int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

}
