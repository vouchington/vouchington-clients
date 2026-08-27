using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ApiFixtureEndpointCoverageTests
{
  private static IReadOnlyDictionary<string, ApiRequest> CreateCrmAndAccountRegistry() =>
      new Dictionary<string, ApiRequest>(StringComparer.Ordinal)
      {
        ["native.crm.contacts.default"] = VouchaApiEndpoints.CrmContacts(new CrmContactsRequest("alice", "new", "credit_cards")),
        ["native.crm.contact-detail.default"] = VouchaApiEndpoints.CrmContact("00000000-0000-7000-8000-000000000584"),
        ["native.crm.contact-create.default"] = VouchaApiEndpoints.CreateCrmContact(
            new CreateCrmContactBody(
                "Alice Creator",
                "alice@example.test",
                Vertical: CrmContactVertical.CreditCards,
                ContactType: CrmContactType.Influencer,
                FollowerCount: 250000,
                Notes: "Creator outreach contact")),
        ["native.crm.contact-update.default"] = VouchaApiEndpoints.UpdateCrmContact(
            "00000000-0000-7000-8000-000000000584",
            new UpdateCrmContactBody(
                "Alice Creator",
                "alice@example.test",
                JsonNullableString.FromString("+1-415-555-0100"),
                JsonNullableCrmContactVertical.FromVertical(CrmContactVertical.CreditCards),
                FollowerCount: JsonNullableInt.FromInt(255000),
                Notes: JsonNullableString.FromString("Updated outreach notes"))),
        ["native.crm.contact-archive.default"] = VouchaApiEndpoints.ArchiveCrmContact("00000000-0000-7000-8000-000000000584"),
        ["native.crm.contact-emails.default"] = VouchaApiEndpoints.CrmContactEmails("00000000-0000-7000-8000-000000000584"),
        ["native.crm.contact-email-send.default"] = VouchaApiEndpoints.SendCrmEmail(
            "00000000-0000-7000-8000-000000000584",
            new SendCrmEmailBody(
                "Warm intro",
                BodyText: "Hi Alice, great to connect.",
                EmailProvider: CrmEmailProvider.Ses,
                AiPrompt: "Write a warm intro",
                AiGeneratedAt: new DateTimeOffset(2026, 7, 1, 12, 29, 30, TimeSpan.Zero))),
        ["native.crm.contact-notes.default"] = VouchaApiEndpoints.CrmContactNotes("00000000-0000-7000-8000-000000000584"),
        ["native.crm.contact-note-create.default"] = VouchaApiEndpoints.CreateCrmNote(
            "00000000-0000-7000-8000-000000000584",
            new CreateCrmNoteBody("Met at the conference")),
        ["native.crm.contact-note-delete.default"] = VouchaApiEndpoints.DeleteCrmNote(
            "00000000-0000-7000-8000-000000000584",
            "00000000-0000-7000-8000-000000000901"),
        ["native.crm.contact-link-user.default"] = VouchaApiEndpoints.LinkCrmContactToUser(
            "00000000-0000-7000-8000-000000000584",
            new LinkCrmContactToUserBody("00000000-0000-7000-8000-000000000001")),
        ["native.crm.contact-unlink-user.default"] =
            VouchaApiEndpoints.UnlinkCrmContactFromUser("00000000-0000-7000-8000-000000000584"),
        ["native.crm.contact-email-draft.default"] = VouchaApiEndpoints.GenerateCrmEmailDraft(
            "00000000-0000-7000-8000-000000000584",
            new GenerateCrmEmailDraftBody("Focus on travel content", "friendly")),
        ["native.crm.import.success.default"] = VouchaApiEndpoints.ImportCrmContacts(
            new ImportCrmContactsBody("name,email\nAlice Creator,alice@example.test")),
        ["native.crm.import.validation.default"] = VouchaApiEndpoints.ImportCrmContacts(
            new ImportCrmContactsBody("name,email,follower_count\nAlice Creator,alice@example.test,-1")),
        ["swift.posts.feed.default"] = VouchaApiEndpoints.Posts(),
        ["swift.notifications.default"] = VouchaApiEndpoints.Notifications(),
        ["native.notifications.redirect-target.default"] = VouchaApiEndpoints.NotificationRedirectTarget("notification-1"),
        ["swift.users.following.default"] = VouchaApiEndpoints.UserFollowing("user-abc"),
        ["swift.users.followers.default"] = VouchaApiEndpoints.UserFollowers("user-abc"),
        ["native.users.followers.search"] = VouchaApiEndpoints.UserFollowers("user-abc", 25, query: "al"),
        ["native.posts.followers.share"] = VouchaApiEndpoints.SharePostWithFollowers("post-abc"),
        ["native.posts.followers.send-selected"] = VouchaApiEndpoints.SendPostToFollowers(
            "post-abc",
            FollowerDistributionBody.Selected(["01900000-0000-7000-8000-000000000502"])),
        ["native.rss-feed-items.followers.share"] =
            VouchaApiEndpoints.ShareRssFeedItemWithFollowers("01900000-0000-7000-8000-000000000503"),
        ["native.rss-feed-items.followers.send-selected"] =
            VouchaApiEndpoints.SendRssFeedItemToFollowers(
                "01900000-0000-7000-8000-000000000503",
                FollowerDistributionBody.Selected(["01900000-0000-7000-8000-000000000502"])),
        ["swift.my.identity.default"] = VouchaApiEndpoints.MyIdentity(),
        ["native.my.email-preferences.default"] = VouchaApiEndpoints.EmailPreferences(),
        ["native.my.email-addresses.empty"] = VouchaApiEndpoints.EmailAddresses(
            "fixture-owner-scoped-email-cursor", 1),
        ["native.my.api-keys.paginated"] = VouchaApiEndpoints.ApiKeys(
            "fixture-owner-scoped-api-key-cursor", 1),
        ["native.my.push-subscriptions.paginated"] = VouchaApiEndpoints.PushSubscriptions(
            "fixture-owner-scoped-push-cursor", 1),
        ["native.my.email-addresses.request.default"] = VouchaApiEndpoints.RequestEmailAddressVerification(" Tests+Native-User@Voucha.ai "),
        ["native.my.email-addresses.verify.default"] = VouchaApiEndpoints.VerifyEmailAddress("tests+native-user@voucha.ai", "ABCD1234"),
        ["native.auth.sessions.default"] = VouchaApiEndpoints.AuthSessions(
            "fixture-owner-scoped-session-cursor", 1),
        ["native.auth.bluesky.link.default"] = VouchaApiEndpoints.BeginBlueskyAccountLink("alice.bsky.social"),
        ["native.auth.bluesky.link.native"] = VouchaApiEndpoints.BeginNativeBlueskyAccountLink(
            "alice.bsky.social", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
        ["native.auth.bluesky.link-completion.default"] = VouchaApiEndpoints.CompleteNativeBlueskyAccountLink(
            "00000000-0000-7000-8000-00000000b501",
            "fixture-completion-token",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
        ["native.fediverse.instances.default"] = VouchaApiEndpoints.FediverseInstances(sort: "best", limit: 25),
        ["native.fediverse.instances.page-2"] = VouchaApiEndpoints.FediverseInstances(after: "next", limit: 25),
        ["native.fediverse.instances.empty"] = VouchaApiEndpoints.FediverseInstances(query: "no-match-fixture-query"),
        ["native.fediverse.instance.slug"] = VouchaApiEndpoints.FediverseInstance("social-example"),
        ["native.fediverse.instance.uuid"] = VouchaApiEndpoints.FediverseInstance("00000000-0000-7000-8000-00000000f003"),
        ["native.auth.bluesky.unlink.default"] = VouchaApiEndpoints.DisconnectBlueskyAccount(),
        ["swift.my.profile.default"] = VouchaApiEndpoints.MyProfile(),
        ["swift.rss-feeds.default"] = VouchaApiEndpoints.AllRssFeeds(),
        ["swift.rss-feed-items.feed.default"] = VouchaApiEndpoints.RssFeedItems(mediaType: "video"),
        ["swift.integration.rss-feed-items.video"] = VouchaApiEndpoints.RssFeedItems(mediaType: "video"),
        ["swift.integration.rss-feed-items.audio"] = VouchaApiEndpoints.RssFeedItems(mediaType: "audio"),
        ["swift.podcast-playback-position.default"] = VouchaApiEndpoints.PodcastPlaybackPosition("episode-1"),
        ["swift.podcast-episode-chapters.default"] = VouchaApiEndpoints.PodcastEpisodeChapters("episode-1"),
      };
}
