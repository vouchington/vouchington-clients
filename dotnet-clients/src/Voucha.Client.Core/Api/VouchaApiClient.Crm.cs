using System.Net.Http.Json;
using System.Text.Json;

namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<CrmContactListResponse> FetchCrmContactsAsync(CrmContactsRequest request, CancellationToken cancellationToken = default) =>
      SendAsync<CrmContactListResponse>(VouchaApiEndpoints.CrmContacts(Require(request)), cancellationToken);

  public Task<CrmContactDetailResponse> FetchCrmContactAsync(string contactId, CancellationToken cancellationToken = default) =>
      SendAsync<CrmContactDetailResponse>(VouchaApiEndpoints.CrmContact(contactId), cancellationToken);

  public Task<CrmContactResponse> CreateCrmContactAsync(CreateCrmContactBody body, CancellationToken cancellationToken = default) =>
      SendAsync<CrmContactResponse>(VouchaApiEndpoints.CreateCrmContact(Require(body)), cancellationToken);

  public Task<CrmContactResponse> UpdateCrmContactAsync(
      string contactId,
      UpdateCrmContactBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<CrmContactResponse>(VouchaApiEndpoints.UpdateCrmContact(contactId, Require(body)), cancellationToken);

  public Task ArchiveCrmContactAsync(string contactId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.ArchiveCrmContact(contactId), cancellationToken);

  public Task<CrmEmailListResponse> FetchCrmContactEmailsAsync(
      string contactId,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<CrmEmailListResponse>(VouchaApiEndpoints.CrmContactEmails(contactId, after, limit), cancellationToken);

  public Task<CrmMessageResponse> SendCrmEmailAsync(
      string contactId,
      SendCrmEmailBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<CrmMessageResponse>(VouchaApiEndpoints.SendCrmEmail(contactId, Require(body)), cancellationToken);

  public Task<CrmNoteListResponse> FetchCrmContactNotesAsync(
      string contactId,
      string? after = null,
      int limit = 25,
      CancellationToken cancellationToken = default) =>
      SendAsync<CrmNoteListResponse>(VouchaApiEndpoints.CrmContactNotes(contactId, after, limit), cancellationToken);

  public Task<CrmNoteResponse> CreateCrmNoteAsync(
      string contactId,
      CreateCrmNoteBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<CrmNoteResponse>(VouchaApiEndpoints.CreateCrmNote(contactId, Require(body)), cancellationToken);

  public Task DeleteCrmNoteAsync(string contactId, string noteId, CancellationToken cancellationToken = default) =>
      SendAsync(VouchaApiEndpoints.DeleteCrmNote(contactId, noteId), cancellationToken);

  public Task<CrmContactResponse> LinkCrmContactToUserAsync(
      string contactId,
      LinkCrmContactToUserBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<CrmContactResponse>(VouchaApiEndpoints.LinkCrmContactToUser(contactId, Require(body)), cancellationToken);

  public Task<CrmContactResponse> UnlinkCrmContactFromUserAsync(string contactId, CancellationToken cancellationToken = default) =>
      SendAsync<CrmContactResponse>(VouchaApiEndpoints.UnlinkCrmContactFromUser(contactId), cancellationToken);

  public Task<CrmEmailDraftResponse> GenerateCrmEmailDraftAsync(
      string contactId,
      GenerateCrmEmailDraftBody body,
      CancellationToken cancellationToken = default) =>
      SendAsync<CrmEmailDraftResponse>(VouchaApiEndpoints.GenerateCrmEmailDraft(contactId, Require(body)), cancellationToken);

  public async Task<CrmImportResponse> ImportCrmContactsAsync(string csv, CancellationToken cancellationToken = default)
  {
    var request = VouchaApiEndpoints.ImportCrmContacts(new ImportCrmContactsBody(csv));
    using var message = new HttpRequestMessage(request.Method, BuildUri(request))
    {
      Content = JsonContent.Create(request.Body, options: VouchaApiJson.Options),
    };

    using var response = await httpClient
        .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
        .ConfigureAwait(false);

    var responseBody = response.Content is null
        ? null
        : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.UnprocessableEntity)
    {
      throw new VouchaApiException(response.StatusCode, string.IsNullOrEmpty(responseBody) ? null : responseBody);
    }

    if (string.IsNullOrWhiteSpace(responseBody))
    {
      throw new InvalidOperationException("Voucha API returned an empty CRM import response body.");
    }

    return JsonSerializer.Deserialize<CrmImportResponse>(responseBody, VouchaApiJson.Options)
        ?? throw new InvalidOperationException("Voucha API returned a null CRM import response body.");
  }
}
