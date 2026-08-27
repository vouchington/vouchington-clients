using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Support;

public sealed class ApiStaffSupportService(VouchaApiClient client) : IStaffSupportService
{
  public Task<SupportThreadListResponse> FetchThreadsAsync(string? query, StaffSupportThreadStatusFilter? status, string? after, int limit, CancellationToken cancellationToken = default) => client.FetchStaffSupportThreadsAsync(query, status, after, limit, cancellationToken);
  public Task<SupportThreadResponse> FetchThreadAsync(string threadId, CancellationToken cancellationToken = default) => client.FetchStaffSupportThreadAsync(threadId, cancellationToken);
  public Task<SupportThreadResponse> AssignAsync(string threadId, string administratorId, CancellationToken cancellationToken = default) => client.AssignStaffSupportThreadAsync(threadId, administratorId, cancellationToken);
  public Task<SupportThreadResponse> ResolveAsync(string threadId, bool resolved, CancellationToken cancellationToken = default) => client.ResolveStaffSupportThreadAsync(threadId, resolved, cancellationToken);
  public Task<SupportMessageListResponse> FetchMessagesAsync(string threadId, string? after, int limit, CancellationToken cancellationToken = default) => client.FetchStaffSupportMessagesAsync(threadId, after, limit, cancellationToken);
  public Task<SupportMessageResponse> CreateMessageAsync(string threadId, string bodyText, CancellationToken cancellationToken = default) => client.CreateStaffSupportMessageAsync(threadId, bodyText, cancellationToken);
  public Task<SupportDraftQueuedResponse> QueueDraftAsync(string threadId, CancellationToken cancellationToken = default) => client.QueueStaffSupportDraftAsync(threadId, cancellationToken);
  public Task<SupportMessageResponse> UpdateDraftAsync(string threadId, string messageId, string bodyText, CancellationToken cancellationToken = default) => client.UpdateStaffSupportDraftAsync(threadId, messageId, bodyText, cancellationToken);
  public Task<SupportMessageResponse> ApproveAsync(string threadId, string messageId, CancellationToken cancellationToken = default) => client.ApproveStaffSupportMessageAsync(threadId, messageId, cancellationToken);
  public Task<SupportMessageResponse> SendAsync(string threadId, string messageId, CancellationToken cancellationToken = default) => client.SendStaffSupportMessageAsync(threadId, messageId, cancellationToken);
  public Task<SupportContactListResponse> FetchContactsAsync(string? query, string? after, int limit, CancellationToken cancellationToken = default) => client.FetchStaffSupportContactsAsync(query, after, limit, cancellationToken);
  public Task<SupportContactDetailResponse> FetchContactAsync(string contactId, string? after, int limit, CancellationToken cancellationToken = default) => client.FetchStaffSupportContactAsync(contactId, after, limit, cancellationToken);
}
