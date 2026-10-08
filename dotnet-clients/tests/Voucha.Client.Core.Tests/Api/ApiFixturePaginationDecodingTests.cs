using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ApiFixturePaginationDecodingTests
{
  [Fact]
  public void DecodesOpaqueMessagingContinuationFixtures()
  {
    var conversations = Decode<DirectConversationsResponse>("native.messages.conversations.default");
    var olderConversations = Decode<DirectConversationsResponse>("native.messages.conversations.page-2");
    Assert.True(conversations.PageInfo.HasNextPage);
    Assert.StartsWith("eyJ", conversations.PageInfo.EndCursor, StringComparison.Ordinal);
    Assert.Equal(["00000000-0000-7000-8000-000000000103"], olderConversations.Results.Select(row => row.Id));
    Assert.False(olderConversations.PageInfo.HasNextPage);

    var messages = Decode<DirectMessagesResponse>("native.messages.thread.default");
    var olderMessages = Decode<DirectMessagesResponse>("native.messages.thread.page-2");
    Assert.True(messages.PageInfo.HasNextPage);
    Assert.StartsWith("eyJ", messages.PageInfo.EndCursor, StringComparison.Ordinal);
    Assert.Equal("Earlier context", Assert.Single(olderMessages.Results).BodyText);
    Assert.False(olderMessages.PageInfo.HasNextPage);
  }

  [Fact]
  public void DecodesOpaqueModmailAndSavedReplyContinuationFixtures()
  {
    var threads = Decode<CommunityModmailThreadListResponse>("web.communities.modmail.page-2");
    var messages = Decode<CommunityModmailMessageListResponse>("web.communities.modmail-messages.page-2");
    var replies = Decode<CommunitySavedRepliesResponse>("web.communities.saved-replies.page-2");

    var thread = Assert.Single(threads.Results);
    Assert.Equal("00000000-0000-7000-8000-000000000502", thread.Id);
    Assert.Equal("modmail", thread.ChannelType);
    Assert.Equal(string.Empty, thread.Title);
    Assert.Null(thread.AssignedAt);
    Assert.Null(thread.ResolvedById);
    Assert.Equal("user-1", thread.CreatedById);
    Assert.Equal("Earlier modmail context.", Assert.Single(messages.Results).BodyText);
    Assert.Equal(1, Assert.Single(replies.Results).OrderIndex);
    Assert.False(threads.PageInfo.HasNextPage);
    Assert.False(messages.PageInfo.HasNextPage);
    Assert.False(replies.PageInfo.HasNextPage);
  }

  private static T Decode<T>(string fixtureId) where T : class =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(fixtureId), VouchaApiJson.Options)
      ?? throw new InvalidOperationException($"Fixture {fixtureId} decoded as null.");
}
