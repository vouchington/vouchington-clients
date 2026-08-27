using Voucha.Client.Core.Api;
using Voucha.Client.Core.Messages;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class QueuedDirectMessagesService : IDirectMessagesService
{
  private readonly Dictionary<string, List<TaskCompletionSource<DirectConversationResponse>>> conversations = [];
  private readonly Dictionary<string, List<TaskCompletionSource<DirectMessagesResponse>>> messages = [];
  private readonly Dictionary<string, List<TaskCompletionSource<DirectMessageParticipantsResponse>>> participants = [];
  private readonly Dictionary<string, List<TaskCompletionSource<UsersSearchResponse>>> searches = [];

  public List<string> ConversationRequests { get; } = [];
  public List<FetchDirectConversationMessagesRequest> MessageRequests { get; } = [];
  public List<string> ParticipantRequests { get; } = [];
  public List<SearchUsersRequest> SearchRequests { get; } = [];

  public Task<DirectConversationsResponse> FetchDirectMessagesAsync(
      FetchDirectMessagesRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromException<DirectConversationsResponse>(new NotSupportedException());

  public Task<DirectConversationResponse> FetchDirectConversationAsync(
      string conversationId,
      CancellationToken cancellationToken = default)
  {
    ConversationRequests.Add(conversationId);
    return Enqueue(conversations, conversationId).Task;
  }

  public Task<DirectConversationResponse> CreateDirectConversationAsync(
      CreateDirectConversationRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromException<DirectConversationResponse>(new NotSupportedException());

  public Task<DirectMessagesResponse> FetchDirectConversationMessagesAsync(
      FetchDirectConversationMessagesRequest request,
      CancellationToken cancellationToken = default)
  {
    MessageRequests.Add(request);
    return Enqueue(messages, request.ConversationId).Task;
  }

  public Task<DirectMessageResponse> SendDirectMessageAsync(
      SendDirectMessageRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromException<DirectMessageResponse>(new NotSupportedException());

  public Task<DirectMessageParticipantsResponse> FetchDirectConversationParticipantsAsync(
      string conversationId,
      CancellationToken cancellationToken = default)
  {
    ParticipantRequests.Add(conversationId);
    return Enqueue(participants, conversationId).Task;
  }

  public Task<DirectMessageParticipantResponse> AddDirectConversationParticipantAsync(
      AddDirectConversationParticipantRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromException<DirectMessageParticipantResponse>(new NotSupportedException());

  public Task RemoveDirectConversationParticipantAsync(
      string conversationId,
      string userId,
      CancellationToken cancellationToken = default) =>
      Task.FromException(new NotSupportedException());

  public Task<DirectConversationPolicyResponse> UpdateDirectConversationParticipantPolicyAsync(
      UpdateDirectConversationParticipantPolicyRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromException<DirectConversationPolicyResponse>(new NotSupportedException());

  public Task<UsersSearchResponse> SearchUsersAsync(
      SearchUsersRequest request,
      CancellationToken cancellationToken = default)
  {
    SearchRequests.Add(request);
    return Enqueue(searches, request.Query).Task;
  }

  public void CompleteConversation(int requestIndex, DirectConversationResponse response) =>
      Complete(conversations, response.Conversation.Id, requestIndex, response);

  public void CompleteMessages(int requestIndex, DirectMessagesResponse response) =>
      Complete(messages, response.Results.First().ConversationId, requestIndex, response);

  public void CompleteParticipants(int requestIndex, DirectMessageParticipantsResponse response) =>
      Complete(participants, response.Results.First().ConversationId, requestIndex, response);

  public void CompleteSearch(int requestIndex, UsersSearchResponse response) =>
      Complete(searches, SearchRequests[requestIndex].Query, requestIndex, response);

  private static TaskCompletionSource<T> Enqueue<T>(
      IDictionary<string, List<TaskCompletionSource<T>>> sources,
      string key)
  {
    if (!sources.TryGetValue(key, out var list))
    {
      list = [];
      sources[key] = list;
    }

    var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    list.Add(source);
    return source;
  }

  private static void Complete<T>(
      IDictionary<string, List<TaskCompletionSource<T>>> sources,
      string key,
      int requestIndex,
      T response)
  {
    if (!sources.TryGetValue(key, out var list) || requestIndex < 0 || requestIndex >= list.Count)
    {
      throw new InvalidOperationException($"No pending request {requestIndex} for {key}.");
    }

    list[requestIndex].SetResult(response);
  }
}
