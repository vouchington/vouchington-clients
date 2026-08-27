using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Tests.Support;

internal sealed class StaffSupportTestService : IStaffSupportService
{
  public SupportThread Thread { get; set; } = ThreadFor("thread");
  public IReadOnlyList<SupportThread> ThreadResults { get; set; } = [];
  public IReadOnlyList<SupportContact> ContactResults { get; set; } = [];
  public IReadOnlyList<SupportMessage> MessageResults { get; set; } = [];
  public PageInfo ThreadPageInfo { get; set; } = new(null, false, null);
  public PageInfo ContactPageInfo { get; set; } = new(null, false, null);
  public PageInfo MessagePageInfo { get; set; } = new(null, false, null);
  public SupportContact Contact { get; set; } = ContactFor("contact");
  public IReadOnlyList<SupportThread> ContactThreads { get; set; } = [];
  public PageInfo ContactThreadPageInfo { get; set; } = new(null, false, null);
  public Exception? Failure { get; set; }
  public Exception? QueueFailure { get; set; }
  public Func<string?, StaffSupportThreadStatusFilter?, string?, int, CancellationToken, Task<SupportThreadListResponse>>? ThreadFetch { get; set; }
  public Func<string, string?, int, CancellationToken, Task<SupportMessageListResponse>>? MessageFetch { get; set; }
  public Func<string?, string?, int, CancellationToken, Task<SupportContactListResponse>>? ContactFetch { get; set; }
  public List<string?> ThreadAfters { get; } = [];
  public List<string?> ContactAfters { get; } = [];
  public List<string?> MessageAfters { get; } = [];
  public List<string> Calls { get; } = [];

  public Task<SupportThreadListResponse> FetchThreadsAsync(string? query, StaffSupportThreadStatusFilter? status, string? after, int limit, CancellationToken cancellationToken = default)
  {
    Calls.Add($"threads:{query}:{status}:{limit}"); ThreadAfters.Add(after); return ThreadFetch?.Invoke(query, status, after, limit, cancellationToken) ?? Result(new SupportThreadListResponse(ThreadResults, ThreadPageInfo));
  }
  public Task<SupportThreadResponse> FetchThreadAsync(string threadId, CancellationToken cancellationToken = default) => Result(new SupportThreadResponse(Thread));
  public Task<SupportThreadResponse> AssignAsync(string threadId, string administratorId, CancellationToken cancellationToken = default) { Calls.Add("assign"); return Result(new SupportThreadResponse(Thread with { Status = SupportThreadStatus.Assigned })); }
  public Task<SupportThreadResponse> ResolveAsync(string threadId, bool resolved, CancellationToken cancellationToken = default) { Calls.Add($"resolve:{resolved}"); return Result(new SupportThreadResponse(Thread with { Status = resolved ? SupportThreadStatus.Resolved : SupportThreadStatus.Open })); }
  public Task<SupportMessageListResponse> FetchMessagesAsync(string threadId, string? after, int limit, CancellationToken cancellationToken = default) { MessageAfters.Add(after); return MessageFetch?.Invoke(threadId, after, limit, cancellationToken) ?? Result(new SupportMessageListResponse(MessageResults, MessagePageInfo)); }
  public Task<SupportMessageResponse> CreateMessageAsync(string threadId, string bodyText, CancellationToken cancellationToken = default) { Calls.Add($"create:{bodyText}"); return Result(new SupportMessageResponse(MessageFor("created", threadId, bodyText))); }
  public Task<SupportDraftQueuedResponse> QueueDraftAsync(string threadId, CancellationToken cancellationToken = default)
  {
    Calls.Add("draft");
    return QueueFailure is null ? Result(new SupportDraftQueuedResponse(true)) : Task.FromException<SupportDraftQueuedResponse>(QueueFailure);
  }
  public Task<SupportMessageResponse> UpdateDraftAsync(string threadId, string messageId, string bodyText, CancellationToken cancellationToken = default) { Calls.Add("update"); return Result(new SupportMessageResponse(MessageFor(messageId, threadId, bodyText, true))); }
  public Task<SupportMessageResponse> ApproveAsync(string threadId, string messageId, CancellationToken cancellationToken = default) { Calls.Add("approve"); return Result(new SupportMessageResponse(MessageFor(messageId, threadId, "draft", true, true))); }
  public Task<SupportMessageResponse> SendAsync(string threadId, string messageId, CancellationToken cancellationToken = default) { Calls.Add("send"); return Result(new SupportMessageResponse(MessageFor(messageId, threadId, "draft", true, true, true))); }
  public Task<SupportContactListResponse> FetchContactsAsync(string? query, string? after, int limit, CancellationToken cancellationToken = default) { Calls.Add($"contacts:{query}:{limit}"); ContactAfters.Add(after); return ContactFetch?.Invoke(query, after, limit, cancellationToken) ?? Result(new SupportContactListResponse(ContactResults, ContactPageInfo)); }
  public Task<SupportContactDetailResponse> FetchContactAsync(string contactId, string? after, int limit, CancellationToken cancellationToken = default) => Result(new SupportContactDetailResponse(Contact, ContactThreads, ContactThreadPageInfo));

  private Task<T> Result<T>(T response) => Failure is null ? Task.FromResult(response) : Task.FromException<T>(Failure);
  public static SupportThread ThreadFor(string id, SupportThreadStatus status = SupportThreadStatus.Open) => new(id, "contact", "Subject", null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, null, null, null, status);
  public static SupportContact ContactFor(string id) => new(id, "contact@example.test", "Contact", null, "", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
  public static SupportMessage MessageFor(string id, string threadId, string body, bool drafted = false, bool approved = false, bool sent = false, string direction = "outbound") => new(id, threadId, direction, body, body, DateTimeOffset.UnixEpoch, null, DateTimeOffset.UnixEpoch, null, null, null, null, drafted ? DateTimeOffset.UnixEpoch : null, null, null, approved ? DateTimeOffset.UnixEpoch : null, null, sent ? DateTimeOffset.UnixEpoch : null);
}
