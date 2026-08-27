using Voucha.Client.Core.Api;
using Voucha.Client.Core.Messages;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class RecordingDirectMessagesService : IDirectMessagesService
{
  public List<FetchDirectMessagesRequest> FetchRequests { get; } = [];
  public List<string> ConversationRequests { get; } = [];
  public List<FetchDirectConversationMessagesRequest> MessageFetchRequests { get; } = [];
  public List<CreateDirectConversationRequest> CreateRequests { get; } = [];
  public List<SendDirectMessageRequest> SendRequests { get; } = [];
  public List<string> ParticipantRequests { get; } = [];
  public List<AddDirectConversationParticipantRequest> AddRequests { get; } = [];
  public List<(string ConversationId, string UserId)> RemoveRequests { get; } = [];
  public List<UpdateDirectConversationParticipantPolicyRequest> PolicyRequests { get; } = [];
  public List<SearchUsersRequest> SearchRequests { get; } = [];
  public Exception? FetchDirectMessagesException { get; set; }
  public Exception? FetchDirectConversationException { get; set; }
  public Exception? CreateDirectConversationException { get; set; }
  public Exception? FetchDirectConversationMessagesException { get; set; }
  public Exception? SendDirectMessageException { get; set; }
  public Exception? FetchDirectConversationParticipantsException { get; set; }
  public Exception? AddDirectConversationParticipantException { get; set; }
  public Exception? RemoveDirectConversationParticipantException { get; set; }
  public Exception? UpdateDirectConversationParticipantPolicyException { get; set; }
  public Exception? SearchUsersException { get; set; }

  public DirectConversationsResponse ConversationsResponse { get; set; } =
      new([], new PageInfo(null, false, null));

  public DirectConversationResponse ConversationResponse { get; set; } =
      new(new DirectConversation("c1", "direct", "Direct message", Time(), Time()));

  public DirectMessagesResponse MessagesResponse { get; set; } =
      new([], new PageInfo(null, false, null));

  public DirectMessageResponse SendResponse { get; set; } =
      new(new DirectMessage("m1", "c1", "sent", "me", Time(), "me"));

  public DirectMessageParticipantsResponse ParticipantsResponse { get; set; } =
      new([], new PageInfo(null, false, null));

  public DirectMessageParticipantResponse ParticipantResponse { get; set; } =
      new(new DirectMessageParticipant("p1", "c1", "u1", "member", Time()));

  public DirectConversationPolicyResponse PolicyResponse { get; set; } =
      new("owner_only");

  public UsersSearchResponse UserSearchResponse { get; set; } =
      new([], new PageInfo(null, false, null));

  public Task<DirectConversationsResponse> FetchDirectMessagesAsync(
      FetchDirectMessagesRequest request,
      CancellationToken cancellationToken = default)
  {
    FetchRequests.Add(request);
    if (FetchDirectMessagesException is not null)
    {
      return Task.FromException<DirectConversationsResponse>(FetchDirectMessagesException);
    }

    return Task.FromResult(ConversationsResponse);
  }

  public Task<DirectConversationResponse> FetchDirectConversationAsync(
      string conversationId,
      CancellationToken cancellationToken = default)
  {
    ConversationRequests.Add(conversationId);
    if (FetchDirectConversationException is not null)
    {
      return Task.FromException<DirectConversationResponse>(FetchDirectConversationException);
    }

    return Task.FromResult(ConversationResponse);
  }

  public Task<DirectConversationResponse> CreateDirectConversationAsync(
      CreateDirectConversationRequest request,
      CancellationToken cancellationToken = default)
  {
    CreateRequests.Add(request);
    if (CreateDirectConversationException is not null)
    {
      return Task.FromException<DirectConversationResponse>(CreateDirectConversationException);
    }

    return Task.FromResult(ConversationResponse);
  }

  public Task<DirectMessagesResponse> FetchDirectConversationMessagesAsync(
      FetchDirectConversationMessagesRequest request,
      CancellationToken cancellationToken = default)
  {
    MessageFetchRequests.Add(request);
    if (FetchDirectConversationMessagesException is not null)
    {
      return Task.FromException<DirectMessagesResponse>(FetchDirectConversationMessagesException);
    }

    return Task.FromResult(MessagesResponse);
  }

  public Task<DirectMessageResponse> SendDirectMessageAsync(
      SendDirectMessageRequest request,
      CancellationToken cancellationToken = default)
  {
    SendRequests.Add(request);
    if (SendDirectMessageException is not null)
    {
      return Task.FromException<DirectMessageResponse>(SendDirectMessageException);
    }

    return Task.FromResult(SendResponse);
  }

  public Task<DirectMessageParticipantsResponse> FetchDirectConversationParticipantsAsync(
      string conversationId,
      CancellationToken cancellationToken = default)
  {
    ParticipantRequests.Add(conversationId);
    if (FetchDirectConversationParticipantsException is not null)
    {
      return Task.FromException<DirectMessageParticipantsResponse>(FetchDirectConversationParticipantsException);
    }

    return Task.FromResult(ParticipantsResponse);
  }

  public Task<DirectMessageParticipantResponse> AddDirectConversationParticipantAsync(
      AddDirectConversationParticipantRequest request,
      CancellationToken cancellationToken = default)
  {
    AddRequests.Add(request);
    if (AddDirectConversationParticipantException is not null)
    {
      return Task.FromException<DirectMessageParticipantResponse>(AddDirectConversationParticipantException);
    }

    return Task.FromResult(ParticipantResponse);
  }

  public Task RemoveDirectConversationParticipantAsync(
      string conversationId,
      string userId,
      CancellationToken cancellationToken = default)
  {
    RemoveRequests.Add((conversationId, userId));
    if (RemoveDirectConversationParticipantException is not null)
    {
      return Task.FromException(RemoveDirectConversationParticipantException);
    }

    return Task.CompletedTask;
  }

  public Task<DirectConversationPolicyResponse> UpdateDirectConversationParticipantPolicyAsync(
      UpdateDirectConversationParticipantPolicyRequest request,
      CancellationToken cancellationToken = default)
  {
    PolicyRequests.Add(request);
    if (UpdateDirectConversationParticipantPolicyException is not null)
    {
      return Task.FromException<DirectConversationPolicyResponse>(
          UpdateDirectConversationParticipantPolicyException);
    }

    return Task.FromResult(PolicyResponse);
  }

  public Task<UsersSearchResponse> SearchUsersAsync(
      SearchUsersRequest request,
      CancellationToken cancellationToken = default)
  {
    SearchRequests.Add(request);
    if (SearchUsersException is not null)
    {
      return Task.FromException<UsersSearchResponse>(SearchUsersException);
    }

    return Task.FromResult(UserSearchResponse);
  }

  private static DateTimeOffset Time() => new(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
}
