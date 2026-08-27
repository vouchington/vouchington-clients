using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Support;

public interface IStaffSupportService
{
  Task<SupportThreadListResponse> FetchThreadsAsync(string? query, StaffSupportThreadStatusFilter? status, string? after, int limit, CancellationToken cancellationToken = default);
  Task<SupportThreadResponse> FetchThreadAsync(string threadId, CancellationToken cancellationToken = default);
  Task<SupportThreadResponse> AssignAsync(string threadId, string administratorId, CancellationToken cancellationToken = default);
  Task<SupportThreadResponse> ResolveAsync(string threadId, bool resolved, CancellationToken cancellationToken = default);
  Task<SupportMessageListResponse> FetchMessagesAsync(string threadId, string? after, int limit, CancellationToken cancellationToken = default);
  Task<SupportMessageResponse> CreateMessageAsync(string threadId, string bodyText, CancellationToken cancellationToken = default);
  Task<SupportDraftQueuedResponse> QueueDraftAsync(string threadId, CancellationToken cancellationToken = default);
  Task<SupportMessageResponse> UpdateDraftAsync(string threadId, string messageId, string bodyText, CancellationToken cancellationToken = default);
  Task<SupportMessageResponse> ApproveAsync(string threadId, string messageId, CancellationToken cancellationToken = default);
  Task<SupportMessageResponse> SendAsync(string threadId, string messageId, CancellationToken cancellationToken = default);
  Task<SupportContactListResponse> FetchContactsAsync(string? query, string? after, int limit, CancellationToken cancellationToken = default);
  Task<SupportContactDetailResponse> FetchContactAsync(string contactId, string? after, int limit, CancellationToken cancellationToken = default);
}
