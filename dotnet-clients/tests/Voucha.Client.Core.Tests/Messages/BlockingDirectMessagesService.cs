using Voucha.Client.Core.Api;
using Voucha.Client.Core.Messages;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class BlockingDirectMessagesService : IDirectMessagesService
{
  private readonly Queue<TaskCompletionSource<DirectConversationsResponse>> inboxResponses = [];
  private readonly Dictionary<string, Queue<TaskCompletionSource<DirectConversationResponse>>> conversationResponses = [];
  private readonly Dictionary<string, Queue<TaskCompletionSource<DirectMessagesResponse>>> messageResponses = [];
  private readonly Queue<TaskCompletionSource<DirectConversationResponse>> createResponses = [];
  private readonly Dictionary<string, Queue<TaskCompletionSource<DirectMessageResponse>>> sendResponses = [];
  private readonly Dictionary<string, Queue<TaskCompletionSource<DirectMessageParticipantsResponse>>> participantResponses = [];
  private readonly Dictionary<string, Queue<TaskCompletionSource<DirectMessageParticipantResponse>>> addParticipantResponses = [];
  private readonly Dictionary<string, Queue<TaskCompletionSource<object?>>> removeParticipantResponses = [];
  private readonly Dictionary<string, Queue<TaskCompletionSource<DirectConversationPolicyResponse>>> policyResponses = [];
  private readonly Dictionary<string, Queue<TaskCompletionSource<UsersSearchResponse>>> searchResponses = [];
  private readonly Lock requestGate = new();
  private readonly SemaphoreSlim requestsChanged = new(0);
  public List<FetchDirectMessagesRequest> ConversationFetchRequests { get; } = [];
  public List<string> DirectConversationFetchRequests { get; } = [];
  public List<FetchDirectConversationMessagesRequest> MessageFetchRequests { get; } = [];
  public List<CreateDirectConversationRequest> CreateRequests { get; } = [];
  public List<SendDirectMessageRequest> SendRequests { get; } = [];
  public List<AddDirectConversationParticipantRequest> AddParticipantRequests { get; } = [];
  public List<string> ParticipantFetchRequests { get; } = [];
  public List<(string ConversationId, string UserId)> RemoveParticipantRequests { get; } = [];
  public List<UpdateDirectConversationParticipantPolicyRequest> PolicyRequests { get; } = [];
  public List<SearchUsersRequest> SearchRequests { get; } = [];

  public Task<DirectConversationsResponse> FetchDirectMessagesAsync(
      FetchDirectMessagesRequest request,
      CancellationToken cancellationToken = default) =>
      RecordConversationFetch(request).Task;

  public Task<DirectConversationResponse> FetchDirectConversationAsync(
      string conversationId,
      CancellationToken cancellationToken = default) =>
      RecordDirectConversationFetch(conversationId).Task;

  public Task<DirectConversationResponse> CreateDirectConversationAsync(
      CreateDirectConversationRequest request,
      CancellationToken cancellationToken = default) =>
      RecordCreate(request).Task;

  public Task<DirectMessagesResponse> FetchDirectConversationMessagesAsync(
      FetchDirectConversationMessagesRequest request,
      CancellationToken cancellationToken = default) =>
      RecordMessageFetch(request).Task;

  public Task<DirectMessageResponse> SendDirectMessageAsync(
      SendDirectMessageRequest request,
      CancellationToken cancellationToken = default) =>
      RecordSend(request).Task;

  public Task<DirectMessageParticipantsResponse> FetchDirectConversationParticipantsAsync(
      string conversationId,
      CancellationToken cancellationToken = default) =>
      RecordParticipantFetch(conversationId).Task;

  public Task<DirectMessageParticipantResponse> AddDirectConversationParticipantAsync(
      AddDirectConversationParticipantRequest request,
      CancellationToken cancellationToken = default) =>
      RecordAddParticipant(request).Task;

  public Task RemoveDirectConversationParticipantAsync(
      string conversationId,
      string userId,
      CancellationToken cancellationToken = default) =>
      RecordRemoveParticipant(conversationId, userId).Task;

  public Task<DirectConversationPolicyResponse> UpdateDirectConversationParticipantPolicyAsync(
      UpdateDirectConversationParticipantPolicyRequest request,
      CancellationToken cancellationToken = default) =>
      RecordPolicyUpdate(request).Task;

  public Task<UsersSearchResponse> SearchUsersAsync(
      SearchUsersRequest request,
      CancellationToken cancellationToken = default) =>
      RecordSearch(request).Task;

  public void CompleteSearch(string query, UsersSearchResponse response) =>
      DequeueThreadSource(searchResponses, query).SetResult(response);

  public void FailSearch(string query, Exception exception) =>
      DequeueThreadSource(searchResponses, query).SetException(exception);

  public void CompleteConversation(string conversationId, DirectConversationResponse response) =>
      DequeueThreadSource(conversationResponses, conversationId).SetResult(response);

  public void CompleteMessages(string conversationId, DirectMessagesResponse response) =>
      DequeueThreadSource(messageResponses, conversationId).SetResult(response);

  public void CompleteConversations(DirectConversationsResponse response) =>
      NextConversationFetch().SetResult(response);

  public void CompleteParticipants(string conversationId, DirectMessageParticipantsResponse response) =>
      DequeueThreadSource(participantResponses, conversationId).SetResult(response);

  public async Task WaitForPendingThreadRequestsAsync(
      string conversationId,
      int count,
      CancellationToken cancellationToken)
  {
    while (!HasPendingThreadRequestCount(conversationId, count))
    {
      await requestsChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
  }

  public async Task WaitForPendingInboxRequestsAsync(int count, CancellationToken cancellationToken)
  {
    while (!HasPendingInboxRequestCount(count))
    {
      await requestsChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
  }

  public async Task WaitForPendingSearchRequestsAsync(string query, int count, CancellationToken cancellationToken)
  {
    while (!HasPendingSearchRequestCount(query, count))
    {
      await requestsChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
  }

  public void CompleteCreateConversation(DirectConversationResponse response) =>
      NextCreateConversation().SetResult(response);

  public void FailCreateConversation(Exception exception) =>
      NextCreateConversation().SetException(exception);

  public void CompleteAddParticipant(string conversationId, DirectMessageParticipantResponse response) =>
      NextSource(addParticipantResponses, conversationId).SetResult(response);

  public void FailAddParticipant(string conversationId, Exception exception) =>
      NextSource(addParticipantResponses, conversationId).SetException(exception);

  public void CompleteRemoveParticipant(string conversationId) =>
      NextSource(removeParticipantResponses, conversationId).SetResult(null);

  public void FailRemoveParticipant(string conversationId, Exception exception) =>
      NextSource(removeParticipantResponses, conversationId).SetException(exception);

  public void CompletePolicyUpdate(string conversationId, DirectConversationPolicyResponse response) =>
      NextSource(policyResponses, conversationId).SetResult(response);

  public void FailPolicyUpdate(string conversationId, Exception exception) =>
      NextSource(policyResponses, conversationId).SetException(exception);

  public void CompleteSend(string conversationId, DirectMessageResponse response) =>
      NextSource(sendResponses, conversationId).SetResult(response);

  public void FailSend(string conversationId, Exception exception) =>
      NextSource(sendResponses, conversationId).SetException(exception);

  private TaskCompletionSource<DirectConversationsResponse> RecordConversationFetch(FetchDirectMessagesRequest request)
  {
    TaskCompletionSource<DirectConversationsResponse> source;
    lock (requestGate)
    {
      source = new TaskCompletionSource<DirectConversationsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
      inboxResponses.Enqueue(source);
      ConversationFetchRequests.Add(request);
    }
    requestsChanged.Release();
    return source;
  }

  private TaskCompletionSource<DirectConversationResponse> ConversationSource(string conversationId) =>
      EnqueueThreadSource(conversationResponses, conversationId);

  private TaskCompletionSource<DirectConversationResponse> RecordDirectConversationFetch(string conversationId)
  {
    DirectConversationFetchRequests.Add(conversationId);
    return ConversationSource(conversationId);
  }

  private TaskCompletionSource<DirectConversationResponse> RecordCreate(CreateDirectConversationRequest request)
  {
    CreateRequests.Add(request);
    var source = new TaskCompletionSource<DirectConversationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    createResponses.Enqueue(source);
    return source;
  }

  private TaskCompletionSource<DirectMessagesResponse> MessageSource(string conversationId) =>
      EnqueueThreadSource(messageResponses, conversationId);

  private TaskCompletionSource<DirectMessageResponse> RecordSend(SendDirectMessageRequest request)
  {
    SendRequests.Add(request);
    return EnqueueSource(sendResponses, request.ConversationId);
  }

  private TaskCompletionSource<DirectMessagesResponse> RecordMessageFetch(FetchDirectConversationMessagesRequest request)
  {
    MessageFetchRequests.Add(request);
    return MessageSource(request.ConversationId);
  }

  private TaskCompletionSource<DirectMessageParticipantResponse> RecordAddParticipant(
      AddDirectConversationParticipantRequest request)
  {
    AddParticipantRequests.Add(request);
    return EnqueueSource(addParticipantResponses, request.ConversationId);
  }

  private TaskCompletionSource<DirectMessageParticipantsResponse> RecordParticipantFetch(string conversationId)
  {
    ParticipantFetchRequests.Add(conversationId);
    return EnqueueThreadSource(participantResponses, conversationId);
  }

  private TaskCompletionSource<object?> RecordRemoveParticipant(string conversationId, string userId)
  {
    RemoveParticipantRequests.Add((conversationId, userId));
    return EnqueueSource(removeParticipantResponses, conversationId);
  }

  private TaskCompletionSource<DirectConversationPolicyResponse> RecordPolicyUpdate(
      UpdateDirectConversationParticipantPolicyRequest request)
  {
    PolicyRequests.Add(request);
    return EnqueueSource(policyResponses, request.ConversationId);
  }

  private TaskCompletionSource<UsersSearchResponse> RecordSearch(SearchUsersRequest request)
  {
    TaskCompletionSource<UsersSearchResponse> source;
    lock (requestGate)
    {
      SearchRequests.Add(request);
      source = EnqueueSource(searchResponses, request.Query);
    }
    requestsChanged.Release();
    return source;
  }

  private static TaskCompletionSource<T> EnqueueSource<T>(
      IDictionary<string, Queue<TaskCompletionSource<T>>> sources,
      string conversationId)
  {
    if (!sources.TryGetValue(conversationId, out var queue))
    {
      queue = [];
      sources[conversationId] = queue;
    }

    var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    queue.Enqueue(source);
    return source;
  }

  private TaskCompletionSource<T> EnqueueThreadSource<T>(
      IDictionary<string, Queue<TaskCompletionSource<T>>> sources,
      string conversationId)
  {
    TaskCompletionSource<T> source;
    lock (requestGate)
    {
      source = EnqueueSource(sources, conversationId);
    }
    requestsChanged.Release();
    return source;
  }

  private TaskCompletionSource<T> DequeueThreadSource<T>(
      IDictionary<string, Queue<TaskCompletionSource<T>>> sources,
      string conversationId)
  {
    lock (requestGate)
    {
      return NextSource(sources, conversationId);
    }
  }

  private bool HasPendingThreadRequestCount(string conversationId, int count)
  {
    lock (requestGate)
    {
      return SourceCount(conversationResponses, conversationId) >= count &&
          SourceCount(messageResponses, conversationId) >= count &&
          SourceCount(participantResponses, conversationId) >= count;
    }
  }

  private bool HasPendingInboxRequestCount(int count)
  {
    lock (requestGate)
    {
      return inboxResponses.Count >= count;
    }
  }

  private bool HasPendingSearchRequestCount(string query, int count)
  {
    lock (requestGate)
    {
      return SourceCount(searchResponses, query) >= count;
    }
  }

  private static int SourceCount<T>(
      IReadOnlyDictionary<string, Queue<TaskCompletionSource<T>>> sources,
      string conversationId) =>
      sources.TryGetValue(conversationId, out var queue) ? queue.Count : 0;

  private TaskCompletionSource<DirectConversationsResponse> NextConversationFetch()
  {
    lock (requestGate)
    {
      if (inboxResponses.Count == 0)
      {
        throw new InvalidOperationException("No pending inbox fetch.");
      }

      return inboxResponses.Dequeue();
    }
  }

  private TaskCompletionSource<DirectConversationResponse> NextCreateConversation()
  {
    if (createResponses.Count == 0)
    {
      throw new InvalidOperationException("No pending create conversation request.");
    }

    return createResponses.Dequeue();
  }

  private static TaskCompletionSource<T> NextSource<T>(
      IDictionary<string, Queue<TaskCompletionSource<T>>> sources,
      string conversationId)
  {
    if (!sources.TryGetValue(conversationId, out var queue) || queue.Count == 0)
    {
      throw new InvalidOperationException($"No pending request for {conversationId}.");
    }

    var source = queue.Dequeue();
    if (queue.Count == 0)
    {
      sources.Remove(conversationId);
    }

    return source;
  }

}
