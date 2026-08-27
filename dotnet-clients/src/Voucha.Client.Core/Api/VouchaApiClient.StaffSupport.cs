namespace Voucha.Client.Core.Api;

public sealed partial class VouchaApiClient
{
  public Task<SupportThreadListResponse> FetchStaffSupportThreadsAsync(string? query = null, StaffSupportThreadStatusFilter? status = null, string? after = null, int limit = 30, CancellationToken cancellationToken = default) =>
      SendAsync<SupportThreadListResponse>(VouchaApiEndpoints.StaffSupportThreads(query, status, after, limit), cancellationToken);
  public Task<SupportThreadResponse> FetchStaffSupportThreadAsync(string threadId, CancellationToken cancellationToken = default) =>
      SendAsync<SupportThreadResponse>(VouchaApiEndpoints.StaffSupportThread(threadId), cancellationToken);
  public Task<SupportThreadResponse> AssignStaffSupportThreadAsync(string threadId, string administratorId, CancellationToken cancellationToken = default) =>
      SendAsync<SupportThreadResponse>(VouchaApiEndpoints.AssignStaffSupportThread(threadId, administratorId), cancellationToken);
  public Task<SupportThreadResponse> ResolveStaffSupportThreadAsync(string threadId, bool resolved, CancellationToken cancellationToken = default) =>
      SendAsync<SupportThreadResponse>(VouchaApiEndpoints.ResolveStaffSupportThread(threadId, resolved), cancellationToken);
  public Task<SupportMessageListResponse> FetchStaffSupportMessagesAsync(string threadId, string? after = null, int limit = 50, CancellationToken cancellationToken = default) =>
      SendAsync<SupportMessageListResponse>(VouchaApiEndpoints.StaffSupportMessages(threadId, after, limit), cancellationToken);
  public Task<SupportMessageResponse> CreateStaffSupportMessageAsync(string threadId, string bodyText, CancellationToken cancellationToken = default) =>
      SendAsync<SupportMessageResponse>(VouchaApiEndpoints.CreateStaffSupportMessage(threadId, bodyText), cancellationToken);
  public Task<SupportDraftQueuedResponse> QueueStaffSupportDraftAsync(string threadId, CancellationToken cancellationToken = default) =>
      SendAsync<SupportDraftQueuedResponse>(VouchaApiEndpoints.QueueStaffSupportDraft(threadId), cancellationToken);
  public Task<SupportMessageResponse> UpdateStaffSupportDraftAsync(string threadId, string messageId, string bodyText, CancellationToken cancellationToken = default) =>
      SendAsync<SupportMessageResponse>(VouchaApiEndpoints.UpdateStaffSupportDraft(threadId, messageId, bodyText), cancellationToken);
  public Task<SupportMessageResponse> ApproveStaffSupportMessageAsync(string threadId, string messageId, CancellationToken cancellationToken = default) =>
      SendAsync<SupportMessageResponse>(VouchaApiEndpoints.ApproveStaffSupportMessage(threadId, messageId), cancellationToken);
  public Task<SupportMessageResponse> SendStaffSupportMessageAsync(string threadId, string messageId, CancellationToken cancellationToken = default) =>
      SendAsync<SupportMessageResponse>(VouchaApiEndpoints.SendStaffSupportMessage(threadId, messageId), cancellationToken);
  public Task<SupportContactListResponse> FetchStaffSupportContactsAsync(string? query = null, string? after = null, int limit = 30, CancellationToken cancellationToken = default) =>
      SendAsync<SupportContactListResponse>(VouchaApiEndpoints.StaffSupportContacts(query, after, limit), cancellationToken);
  public Task<SupportContactDetailResponse> FetchStaffSupportContactAsync(string contactId, string? after = null, int limit = 50, CancellationToken cancellationToken = default) =>
      SendAsync<SupportContactDetailResponse>(VouchaApiEndpoints.StaffSupportContact(contactId, after, limit), cancellationToken);
  public Task<SupportContactResponse> UpdateStaffSupportContactAsync(string contactId, UpdateSupportContactBody body, CancellationToken cancellationToken = default) =>
      SendAsync<SupportContactResponse>(VouchaApiEndpoints.UpdateStaffSupportContact(contactId, body), cancellationToken);
}
