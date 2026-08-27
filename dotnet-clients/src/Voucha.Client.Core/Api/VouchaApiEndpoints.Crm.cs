namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest CrmContacts(CrmContactsRequest request) =>
      Get(
          "/api/v1/crm/contacts",
          Query(
              ("q", (request ?? throw new ArgumentNullException(nameof(request))).Query),
              ("status", request.Status),
              ("vertical", request.Vertical),
              ("linked", request.Linked is null ? null : request.Linked.Value ? "true" : "false"),
              ("after", request.After),
              ("limit", request.Limit)));

  public static ApiRequest CrmContact(string contactId) =>
      Get($"/api/v1/crm/contacts/{Path(contactId)}");

  public static ApiRequest CreateCrmContact(CreateCrmContactBody body) =>
      new(HttpMethod.Post, "/api/v1/crm/contacts") { Body = body };

  public static ApiRequest UpdateCrmContact(string contactId, UpdateCrmContactBody body) =>
      new(HttpMethod.Patch, $"/api/v1/crm/contacts/{Path(contactId)}") { Body = body };

  public static ApiRequest ArchiveCrmContact(string contactId) =>
      new(HttpMethod.Delete, $"/api/v1/crm/contacts/{Path(contactId)}");

  public static ApiRequest CrmContactEmails(string contactId, string? after = null, int limit = 25) =>
      Get($"/api/v1/crm/contacts/{Path(contactId)}/emails", Query(("after", after), ("limit", limit)));

  public static ApiRequest SendCrmEmail(string contactId, SendCrmEmailBody body) =>
      new(HttpMethod.Post, $"/api/v1/crm/contacts/{Path(contactId)}/emails") { Body = body };

  public static ApiRequest CrmContactNotes(string contactId, string? after = null, int limit = 25) =>
      Get($"/api/v1/crm/contacts/{Path(contactId)}/notes", Query(("after", after), ("limit", limit)));

  public static ApiRequest CreateCrmNote(string contactId, CreateCrmNoteBody body) =>
      new(HttpMethod.Post, $"/api/v1/crm/contacts/{Path(contactId)}/notes") { Body = body };

  public static ApiRequest DeleteCrmNote(string contactId, string noteId) =>
      new(HttpMethod.Delete, $"/api/v1/crm/contacts/{Path(contactId)}/notes/{Path(noteId)}");

  public static ApiRequest LinkCrmContactToUser(string contactId, LinkCrmContactToUserBody body) =>
      new(HttpMethod.Put, $"/api/v1/crm/contacts/{Path(contactId)}/user-link") { Body = body };

  public static ApiRequest UnlinkCrmContactFromUser(string contactId) =>
      new(HttpMethod.Delete, $"/api/v1/crm/contacts/{Path(contactId)}/user-link");

  public static ApiRequest GenerateCrmEmailDraft(string contactId, GenerateCrmEmailDraftBody body) =>
      new(HttpMethod.Post, $"/api/v1/crm/contacts/{Path(contactId)}/email-drafts") { Body = body };

  public static ApiRequest ImportCrmContacts(ImportCrmContactsBody body) =>
      new(HttpMethod.Post, "/api/v1/imports/crm-contacts") { Body = body };
}
