using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Crm;

public interface ICrmService
{
  Task<CrmContactListResponse> FetchContactsAsync(CrmContactsRequest request, CancellationToken cancellationToken = default);
  Task<CrmContactDetailResponse> FetchContactAsync(string contactId, CancellationToken cancellationToken = default);
  Task<CrmContactResponse> CreateContactAsync(CreateCrmContactBody body, CancellationToken cancellationToken = default);
  Task<CrmContactResponse> UpdateContactAsync(string contactId, UpdateCrmContactBody body, CancellationToken cancellationToken = default);
  Task ArchiveContactAsync(string contactId, CancellationToken cancellationToken = default);
  Task<CrmEmailListResponse> FetchContactEmailsAsync(string contactId, string? after = null, int limit = 25, CancellationToken cancellationToken = default);
  Task<CrmMessageResponse> SendEmailAsync(string contactId, SendCrmEmailBody body, CancellationToken cancellationToken = default);
  Task<CrmNoteListResponse> FetchContactNotesAsync(string contactId, string? after = null, int limit = 25, CancellationToken cancellationToken = default);
  Task<CrmNoteResponse> CreateNoteAsync(string contactId, CreateCrmNoteBody body, CancellationToken cancellationToken = default);
  Task DeleteNoteAsync(string contactId, string noteId, CancellationToken cancellationToken = default);
  Task<CrmContactResponse> LinkUserAsync(string contactId, LinkCrmContactToUserBody body, CancellationToken cancellationToken = default);
  Task<CrmContactResponse> UnlinkUserAsync(string contactId, CancellationToken cancellationToken = default);
  Task<CrmEmailDraftResponse> GenerateEmailDraftAsync(string contactId, GenerateCrmEmailDraftBody body, CancellationToken cancellationToken = default);
  Task<CrmImportResponse> ImportContactsAsync(string csv, CancellationToken cancellationToken = default);
}
