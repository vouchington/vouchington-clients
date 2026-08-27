namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest StaffSupportThreads(string? query = null, StaffSupportThreadStatusFilter? status = null, string? after = null, int limit = 30) =>
      Get("/api/v1/support/threads", Query(("q", query), ("status", status is { } value ? value.ToWireValue() : null), ("after", after), ("limit", limit)));
  public static ApiRequest StaffSupportThread(string threadId) => Get($"/api/v1/support/threads/{Path(threadId)}");
  public static ApiRequest AssignStaffSupportThread(string threadId, string administratorId) =>
      new(HttpMethod.Patch, $"/api/v1/support/threads/{Path(threadId)}") { Body = new AssignSupportThreadBody(administratorId) };
  public static ApiRequest ResolveStaffSupportThread(string threadId, bool resolved) =>
      new(HttpMethod.Patch, $"/api/v1/support/threads/{Path(threadId)}") { Body = new ResolveSupportThreadBody(resolved) };
  public static ApiRequest StaffSupportMessages(string threadId, string? after = null, int limit = 50) =>
      Get($"/api/v1/support/threads/{Path(threadId)}/messages", Query(("after", after), ("limit", limit)));
  public static ApiRequest CreateStaffSupportMessage(string threadId, string bodyText) =>
      new(HttpMethod.Post, $"/api/v1/support/threads/{Path(threadId)}/messages") { Body = new SupportMessageBody(bodyText) };
  public static ApiRequest QueueStaffSupportDraft(string threadId) =>
      new(HttpMethod.Post, $"/api/v1/support/threads/{Path(threadId)}/drafts");
  public static ApiRequest UpdateStaffSupportDraft(string threadId, string messageId, string bodyText) =>
      new(HttpMethod.Patch, $"/api/v1/support/threads/{Path(threadId)}/messages/{Path(messageId)}") { Body = new SupportMessageBody(bodyText) };
  public static ApiRequest ApproveStaffSupportMessage(string threadId, string messageId) =>
      new(HttpMethod.Post, $"/api/v1/support/threads/{Path(threadId)}/messages/{Path(messageId)}/approvals");
  public static ApiRequest SendStaffSupportMessage(string threadId, string messageId) =>
      new(HttpMethod.Post, $"/api/v1/support/threads/{Path(threadId)}/messages/{Path(messageId)}/sends");
  public static ApiRequest StaffSupportContacts(string? query = null, string? after = null, int limit = 30) =>
      Get("/api/v1/support/contacts", Query(("q", query), ("after", after), ("limit", limit)));
  public static ApiRequest StaffSupportContact(string contactId, string? after = null, int limit = 50) =>
      Get($"/api/v1/support/contacts/{Path(contactId)}", Query(("after", after), ("limit", limit)));
  public static ApiRequest UpdateStaffSupportContact(string contactId, UpdateSupportContactBody body) =>
      new(HttpMethod.Patch, $"/api/v1/support/contacts/{Path(contactId)}") { Body = body };
}
