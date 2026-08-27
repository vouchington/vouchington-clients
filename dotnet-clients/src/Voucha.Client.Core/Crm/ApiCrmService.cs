using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Crm;

public sealed class ApiCrmService : ICrmService
{
  private readonly VouchaApiClient client;

  public ApiCrmService(VouchaApiClient client) =>
      this.client = client ?? throw new ArgumentNullException(nameof(client));

  public Task<CrmContactListResponse> FetchContactsAsync(CrmContactsRequest request, CancellationToken cancellationToken = default) =>
      client.FetchCrmContactsAsync(request, cancellationToken);

  public Task<CrmContactDetailResponse> FetchContactAsync(string contactId, CancellationToken cancellationToken = default) =>
      client.FetchCrmContactAsync(contactId, cancellationToken);

  public Task<CrmContactResponse> CreateContactAsync(CreateCrmContactBody body, CancellationToken cancellationToken = default) =>
      client.CreateCrmContactAsync(body, cancellationToken);

  public Task<CrmContactResponse> UpdateContactAsync(string contactId, UpdateCrmContactBody body, CancellationToken cancellationToken = default) =>
      client.UpdateCrmContactAsync(contactId, body, cancellationToken);

  public Task ArchiveContactAsync(string contactId, CancellationToken cancellationToken = default) =>
      client.ArchiveCrmContactAsync(contactId, cancellationToken);

  public Task<CrmEmailListResponse> FetchContactEmailsAsync(string contactId, string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      client.FetchCrmContactEmailsAsync(contactId, after, limit, cancellationToken);

  public Task<CrmMessageResponse> SendEmailAsync(string contactId, SendCrmEmailBody body, CancellationToken cancellationToken = default) =>
      client.SendCrmEmailAsync(contactId, body, cancellationToken);

  public Task<CrmNoteListResponse> FetchContactNotesAsync(string contactId, string? after = null, int limit = 25, CancellationToken cancellationToken = default) =>
      client.FetchCrmContactNotesAsync(contactId, after, limit, cancellationToken);

  public Task<CrmNoteResponse> CreateNoteAsync(string contactId, CreateCrmNoteBody body, CancellationToken cancellationToken = default) =>
      client.CreateCrmNoteAsync(contactId, body, cancellationToken);

  public Task DeleteNoteAsync(string contactId, string noteId, CancellationToken cancellationToken = default) =>
      client.DeleteCrmNoteAsync(contactId, noteId, cancellationToken);

  public Task<CrmContactResponse> LinkUserAsync(string contactId, LinkCrmContactToUserBody body, CancellationToken cancellationToken = default) =>
      client.LinkCrmContactToUserAsync(contactId, body, cancellationToken);

  public Task<CrmContactResponse> UnlinkUserAsync(string contactId, CancellationToken cancellationToken = default) =>
      client.UnlinkCrmContactFromUserAsync(contactId, cancellationToken);

  public Task<CrmEmailDraftResponse> GenerateEmailDraftAsync(string contactId, GenerateCrmEmailDraftBody body, CancellationToken cancellationToken = default) =>
      client.GenerateCrmEmailDraftAsync(contactId, body, cancellationToken);

  public Task<CrmImportResponse> ImportContactsAsync(string csv, CancellationToken cancellationToken = default) =>
      client.ImportCrmContactsAsync(csv, cancellationToken);
}
