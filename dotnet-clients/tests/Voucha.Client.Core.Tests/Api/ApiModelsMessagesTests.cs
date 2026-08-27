using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class ApiModelsMessagesTests
{
  [Fact]
  public void RequestModelsSerializeWithWebNaming()
  {
    Assert.Equal(
        "{\"after\":\"cursor-1\",\"limit\":3}",
        JsonSerializer.Serialize(new FetchDirectMessagesRequest("cursor-1", 3), VouchaApiJson.Options));
    Assert.Equal(
        "{\"conversationId\":\"c1\",\"after\":\"cursor-2\",\"limit\":4}",
        JsonSerializer.Serialize(
            new FetchDirectConversationMessagesRequest("c1", "cursor-2", 4),
            VouchaApiJson.Options));
    Assert.Equal(
        "{\"userIds\":[\"u1\",\"u2\"]}",
        JsonSerializer.Serialize(new CreateDirectConversationRequest(["u1", "u2"]), VouchaApiJson.Options));
    Assert.Equal(
        "{\"conversationId\":\"c1\",\"text\":\"hello\"}",
        JsonSerializer.Serialize(new SendDirectMessageRequest("c1", "hello"), VouchaApiJson.Options));
    Assert.Equal(
        "{\"conversationId\":\"c1\",\"userId\":\"u2\"}",
        JsonSerializer.Serialize(
            new AddDirectConversationParticipantRequest("c1", "u2"),
            VouchaApiJson.Options));
    Assert.Equal(
        "{\"conversationId\":\"c1\",\"participantAddPolicy\":\"all_members\"}",
        JsonSerializer.Serialize(
            new UpdateDirectConversationParticipantPolicyRequest("c1", "all_members"),
            VouchaApiJson.Options));
    Assert.Equal(
        "{\"query\":\"alice\",\"limit\":8}",
        JsonSerializer.Serialize(new SearchUsersRequest("alice", Limit: 8), VouchaApiJson.Options));
    Assert.Equal(
        "{\"query\":\"alice\",\"after\":\"cursor-1\",\"limit\":8}",
        JsonSerializer.Serialize(new SearchUsersRequest("alice", "cursor-1", 8), VouchaApiJson.Options));
  }

  [Fact]
  public void ResponseModelsDeserializeWithSnakeCaseNaming()
  {
    var conversation = JsonSerializer.Deserialize<DirectConversationResponse>(
        """
        {
          "conversation": {
            "id": "c1",
            "channel_type": "direct",
            "title": "Alice, Bob",
            "created_at": "2026-07-01T10:00:00Z",
            "updated_at": "2026-07-01T10:00:00Z",
            "created_by_id": "u1",
            "participant_usernames": ["alice", "bob"],
            "participant_add_policy": "all_members",
            "participants": [
              {
                "id": "p1",
                "conversation_id": "c1",
                "user_id": "u1",
                "role": "owner",
                "created_at": "2026-07-01T10:00:00Z",
                "username": "alice",
                "profile_image_id": "image-1"
              }
            ]
          }
        }
        """,
        VouchaApiJson.Options);

    var messages = JsonSerializer.Deserialize<DirectMessagesResponse>(
        """
        {
          "results": [
            {
              "id": "m1",
              "conversation_id": "c1",
              "body_text": "hello",
              "created_by_id": "u1",
              "created_at": "2026-07-01T10:00:00Z",
              "sender_username": "alice",
              "updated_at": "2026-07-01T10:05:00Z",
              "deleted_at": null
            }
          ],
          "page_info": {
            "end_cursor": "cursor-1",
            "has_next_page": true,
            "start_cursor": "cursor-0",
            "has_more": true
          }
        }
        """,
        VouchaApiJson.Options);

    var participants = JsonSerializer.Deserialize<DirectMessageParticipantsResponse>(
        """
        {
          "results": [
            {
              "id": "p1",
              "conversation_id": "c1",
              "user_id": "u1",
              "role": "owner",
              "created_at": "2026-07-01T10:00:00Z",
              "removed_at": null,
              "username": "alice",
              "profile_image_id": "image-1"
            }
          ],
          "page_info": {
            "end_cursor": null,
            "has_next_page": false,
            "start_cursor": null
          }
        }
        """,
        VouchaApiJson.Options);

    var participant = JsonSerializer.Deserialize<DirectMessageParticipantResponse>(
        """
        {
          "participant": {
            "id": "p1",
            "conversation_id": "c1",
            "user_id": "u1",
            "role": "owner",
            "created_at": "2026-07-01T10:00:00Z",
            "username": "alice",
            "profile_image_id": "image-1"
          }
        }
        """,
        VouchaApiJson.Options);

    var policy = JsonSerializer.Deserialize<DirectConversationPolicyResponse>(
        """{"participant_add_policy":"all_members"}""",
        VouchaApiJson.Options);

    var users = JsonSerializer.Deserialize<UsersSearchResponse>(
        """
        {
          "results": [
            {
              "id": "u1",
              "username": "alice",
              "roles": []
            }
          ],
          "page_info": {
            "end_cursor": null,
            "has_next_page": false,
            "start_cursor": null
          }
        }
        """,
        VouchaApiJson.Options);

    Assert.Equal("c1", conversation!.Conversation.Id);
    Assert.Equal("all_members", conversation.Conversation.ParticipantAddPolicy);
    Assert.Equal("alice", conversation.Conversation.Participants![0].Username);

    Assert.Equal("m1", messages!.Results[0].Id);
    Assert.Equal("alice", messages.Results[0].SenderUsername);
    Assert.True(messages.PageInfo.HasMore);

    Assert.Equal("p1", participants!.Results[0].Id);
    Assert.Equal("alice", participants.Results[0].Username);

    Assert.Equal("owner", participant!.Participant.Role);
    Assert.Equal("all_members", policy!.ParticipantAddPolicy);
    Assert.Equal("alice", users!.Results[0].Username);
  }

  [Fact]
  public void UsersSearchResponseDecodesAdminExtrasAndIgnoresUnknownKeys()
  {
    var users = JsonSerializer.Deserialize<UsersSearchResponse>(
        """
        {
          "results": [
            {
              "id": "u1",
              "username": "alice",
              "email_address": "alice@example.com",
              "suspended_at": "2026-01-02T03:04:05Z",
              "membership_plan": "pro"
            }
          ],
          "page_info": { "end_cursor": null, "has_next_page": false, "start_cursor": null }
        }
        """,
        VouchaApiJson.Options);

    Assert.Equal("alice@example.com", users!.Results[0].EmailAddress);
    Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), users.Results[0].SuspendedAt);
  }

  [Fact]
  public void UserSearchResultMirrorsPublicUserPublicFields()
  {
    const string json = """
        {
          "id": "u1",
          "username": "alice",
          "name": "Alice",
          "profile_image_id": "img-1",
          "markdown": "bio",
          "use_display_name_from": "username",
          "is_official_account": true,
          "verification_status": "verified",
          "verified_badge_visible": true,
          "verified_display_name": "Alice A",
          "public_verified_name_display": "full_name",
          "roles": ["member"],
          "display_account": { "id": "acct-1", "name": "Alice" },
          "lingua_rs_detected_language": "en",
          "__entity_type": "user"
        }
        """;
    var publicUser = JsonSerializer.Deserialize<PublicUser>(json, VouchaApiJson.Options)!;
    var searchResult = JsonSerializer.Deserialize<UserSearchResult>(json, VouchaApiJson.Options)!;
    Assert.Equal(publicUser.Id, searchResult.Id);
    Assert.Equal(publicUser.Username, searchResult.Username);
    Assert.Equal(publicUser.Name, searchResult.Name);
    Assert.Equal(publicUser.ProfileImageId, searchResult.ProfileImageId);
    Assert.Equal(publicUser.Markdown, searchResult.Markdown);
    Assert.Equal(publicUser.UseDisplayNameFrom, searchResult.UseDisplayNameFrom);
    Assert.Equal(publicUser.IsOfficialAccount, searchResult.IsOfficialAccount);
    Assert.Equal(publicUser.VerificationStatus, searchResult.VerificationStatus);
    Assert.Equal(publicUser.VerifiedBadgeVisible, searchResult.VerifiedBadgeVisible);
    Assert.Equal(publicUser.VerifiedDisplayName, searchResult.VerifiedDisplayName);
    Assert.Equal(publicUser.PublicVerifiedNameDisplay, searchResult.PublicVerifiedNameDisplay);
    Assert.Equal(publicUser.Roles, searchResult.Roles);
    Assert.Equal(publicUser.DisplayAccount?.Id, searchResult.DisplayAccount?.Id);
    Assert.Equal(publicUser.DisplayAccount?.Name, searchResult.DisplayAccount?.Name);
    Assert.Equal(publicUser.LinguaRsDetectedLanguage, searchResult.LinguaRsDetectedLanguage);
    Assert.Equal(publicUser.EntityType, searchResult.EntityType);
    Assert.Null(searchResult.EmailAddress);
    Assert.Null(searchResult.SuspendedAt);
  }
}
