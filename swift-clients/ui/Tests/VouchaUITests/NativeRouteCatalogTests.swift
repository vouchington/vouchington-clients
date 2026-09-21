@testable import VouchaFeatures
import VouchaLocalization
import XCTest

final class NativeRouteCatalogTests: XCTestCase {
    func testIncludedFamiliesResolveToNativeDestinations() throws {
        let checker = NativeRouteParityChecker()

        for entry in NativeRouteCatalog.includedEntries {
            guard case let .included(destination) = entry.kind else {
                XCTFail("Expected included entry for \(entry.auditFamily)")
                continue
            }

            XCTAssertEqual(
                checker.destinationIdentifier(for: entry.representativePath),
                destination,
                "Representative path failed for \(entry.auditFamily)"
            )
            let familyText = try XCTUnwrap(entry.presentationFamilyText)
            XCTAssertFalse(UiMessages.string(familyText, locale: .english).isEmpty)
        }
    }

    func testIncludedFamilyPresentationUsesLocalizedDestinationMetadata() throws {
        let news = try XCTUnwrap(
            NativeRouteCatalog.includedEntries.first { $0.destinationIdentifier == .feedNews }
        )

        let familyText = try XCTUnwrap(news.presentationFamilyText)

        XCTAssertEqual(UiMessages.string(familyText, locale: .english), "News feed")
        XCTAssertEqual(UiMessages.string(familyText, locale: .french), "Fil d'actualité")
    }

    func testExcludedFamiliesDoNotResolveToNativeDestinations() {
        let checker = NativeRouteParityChecker()

        for entry in NativeRouteCatalog.excludedEntries {
            XCTAssertNil(
                checker.destinationIdentifier(for: entry.representativePath),
                "Excluded path unexpectedly mapped for \(entry.auditFamily)"
            )

            switch checker.resolution(for: entry.representativePath) {
            case let .excluded(family, reason, pattern):
                XCTAssertEqual(family, entry.auditFamily)
                XCTAssertEqual(reason, entry.exclusionReason)
                XCTAssertFalse(pattern.isEmpty)
            default:
                XCTFail("Expected excluded resolution for \(entry.auditFamily)")
            }
        }
    }

    func testExcludedAuditMetadataCannotBecomePresentationText() throws {
        let excluded = try XCTUnwrap(NativeRouteCatalog.excludedEntries.first)

        XCTAssertNil(excluded.presentationFamilyText)
        XCTAssertNotNil(excluded.exclusionAuditMetadata)
    }

    func testCatalogCoversRepresentativeRoutesFromDocs() {
        let checker = NativeRouteParityChecker()

        XCTAssertEqual(checker.destinationIdentifier(for: "/feed/posts?sort=new"), .feedPosts)
        XCTAssertEqual(checker.destinationIdentifier(for: "/feed/podcasts/sources"), .feedPodcasts)
        XCTAssertEqual(checker.destinationIdentifier(for: "/feed/videos/topics"), .feedVideos)
        XCTAssertEqual(checker.destinationIdentifier(for: "/feed/referral-links/mutual"), .feedReferralLinks)
        XCTAssertEqual(checker.destinationIdentifier(for: "/web-search?q=swift"), .webSearch)
        XCTAssertEqual(checker.destinationIdentifier(for: "/fediverse?q=swift"), .fediverseSearch)
        XCTAssertEqual(checker.destinationIdentifier(for: "/instances"), .fediverseInstances)
        XCTAssertEqual(checker.destinationIdentifier(for: "/instance/example.social"), .topicDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/posts"), .postsBrowse)
        XCTAssertEqual(checker.destinationIdentifier(for: "/links"), .postsBrowse)
        XCTAssertEqual(checker.destinationIdentifier(for: "/review/abc123/edit"), .postDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/link/abc123"), .postDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/story/story-1"), .postDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/article/community-guidelines"), .postDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/reviews/create"), .postCompose)
        XCTAssertEqual(checker.destinationIdentifier(for: "/links/create"), .postCompose)
        XCTAssertEqual(checker.destinationIdentifier(for: "/articles/create"), .postCompose)
        XCTAssertEqual(checker.destinationIdentifier(for: "/blog/create"), .postCompose)
        XCTAssertNil(checker.destinationIdentifier(for: "/stories/create"))
        XCTAssertEqual(checker.destinationIdentifier(for: "/news"), .feedNews)
        XCTAssertEqual(checker.destinationIdentifier(for: "/videos"), .feedVideos)
        XCTAssertEqual(checker.destinationIdentifier(for: "/podcast-episodes"), .feedPodcasts)
        XCTAssertEqual(checker.destinationIdentifier(for: "/podcasts/business"), .sourcesBrowse)
        XCTAssertEqual(checker.destinationIdentifier(for: "/discussion/abc123/comment/comment-1"), .postDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/data-point/abc123/tags/topic"), .postDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/topic/abc123/discussions"), .topicDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/topic/abc123/posts"), .topicDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/source/example/latest"), .sourceDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/source/example/tags/topic"), .sourceDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/topic/abc123/tags/publisher_type"), .topicDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/source/example/crawls/crawl-1"), .sourceDetail)
        XCTAssertNil(checker.destinationIdentifier(for: "/rss-feed-items/abc123"))
        XCTAssertEqual(checker.destinationIdentifier(for: "/news-sources"), .sourcesBrowse)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/news-sources"), .sourcesBrowse)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/podcasts"), .sourcesBrowse)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/channels"), .sourcesBrowse)
        XCTAssertEqual(checker.destinationIdentifier(for: "/channels"), .sourcesBrowse)
        XCTAssertNil(checker.destinationIdentifier(for: "/rss-feed-categories"))
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/topics/import-export"), .topicImportExport)
        XCTAssertEqual(checker.destinationIdentifier(for: "/communities/example/news/sources"), .communityDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/communities/example/lists"), .communityDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/landing/alice"), .landingPages)
        XCTAssertEqual(checker.destinationIdentifier(for: "/landing/alice/featured"), .landingPages)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/landing-page/featured/analytics"), .landingPages)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/lists"), .lists)
        XCTAssertEqual(checker.destinationIdentifier(for: "/communities/create"), .communityDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/communities/invite/invite-code"), .communityDetail)
        XCTAssertEqual(
            checker.destinationIdentifier(for: "/communities/example/settings/pinned-posts"),
            .communityDetail
        )
        XCTAssertEqual(checker.destinationIdentifier(for: "/urls"), .urlsBrowse)
        XCTAssertEqual(checker.destinationIdentifier(for: "/url/url-1"), .urlDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/url/url-1/crawls"), .urlDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/url/url-1/crawls/crawl-1"), .urlDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/user/alice/rss-feeds/following"), .userProfile)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/posts/saved"), .bookmarks)
        XCTAssertEqual(
            checker.destinationIdentifier(for: "/my/friend-recommendations"),
            .friendRecommendations
        )
        XCTAssertEqual(
            checker.destinationIdentifier(for: "/my/friend-recommendations/dismissed"),
            .bookmarks
        )
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/news-preferences"), .advancedSettings)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/notification-settings"), .notificationSettings)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/language"), .advancedSettings)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/notifications"), .notifications)
        XCTAssertEqual(checker.destinationIdentifier(for: "/appeals"), .moderationAppeals)
        XCTAssertEqual(checker.destinationIdentifier(for: "/disputes"), .moderationDisputes)
        XCTAssertEqual(checker.destinationIdentifier(for: "/reports"), .moderationReports)
        XCTAssertEqual(checker.destinationIdentifier(for: "/memberships/grants"), .membershipGrants)
        XCTAssertEqual(checker.destinationIdentifier(for: "/user/alice/admin"), .userAdmin)
        XCTAssertEqual(checker.destinationIdentifier(for: "/login"), .signIn)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/account-status"), .accountSettings)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/identity-verification"), .accountSettings)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/profile"), .profileSettings)
        XCTAssertEqual(checker.destinationIdentifier(for: "/messages/abc123"), .messages)
        XCTAssertEqual(checker.destinationIdentifier(for: "/messages/new"), .messages)
        XCTAssertEqual(checker.destinationIdentifier(for: "/messages/modmail/example/thread-1"), .messages)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/news-sources/import-export"), .sourceImportExport)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/podcasts/import-export"), .sourceImportExport)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/channels/import-export"), .sourceImportExport)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/sources/import-export"), .sourceImportExport)
        XCTAssertEqual(checker.destinationIdentifier(for: "/agents"), .engineeringAgents)
        XCTAssertEqual(
            checker.destinationIdentifier(for: "/agent/helper/conversation/conversation-1"),
            .engineeringAgents
        )
        XCTAssertEqual(checker.destinationIdentifier(for: "/admin/queues"), .engineeringQueues)
        XCTAssertEqual(checker.destinationIdentifier(for: "/admin/postgresql"), .engineeringPostgresql)
        XCTAssertEqual(checker.destinationIdentifier(for: "/admin/valkey"), .engineeringValkey)
        XCTAssertEqual(checker.destinationIdentifier(for: "/admin/ai-costs"), .engineeringAiCosts)
        XCTAssertEqual(checker.destinationIdentifier(for: "/admin/dynamic-config"), .engineeringDynamicConfig)
        XCTAssertEqual(checker.destinationIdentifier(for: "/growth"), .growthDashboard)
        XCTAssertEqual(checker.destinationIdentifier(for: "/posts/review-queue"), .moderationReviewQueue)
        XCTAssertEqual(checker.destinationIdentifier(for: "/admin/modlog"), .moderationAdmin)
        XCTAssertEqual(checker.destinationIdentifier(for: "/admin/moderation-analytics"), .moderationAdmin)
        XCTAssertEqual(checker.destinationIdentifier(for: "/vote-integrity/flags"), .moderationIntegrity)
        XCTAssertEqual(checker.destinationIdentifier(for: "/report-integrity/flags"), .moderationIntegrity)
        XCTAssertEqual(checker.destinationIdentifier(for: "/vote-integrity/penalties"), .moderationIntegrity)
        XCTAssertEqual(checker.destinationIdentifier(for: "/report-integrity/penalties"), .moderationIntegrity)
        XCTAssertEqual(checker.destinationIdentifier(for: "/topic-recommendations/create"), .topicRecommendations)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/disputes"), .moderationCases)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/warnings"), .moderationCases)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/bans"), .moderationCases)
        XCTAssertEqual(checker.destinationIdentifier(for: "/my/removed-posts"), .moderationCases)
        XCTAssertEqual(checker.destinationIdentifier(for: "/moderation-transparency"), .moderationTransparency)
        XCTAssertEqual(checker.destinationIdentifier(for: "/plans"), .plans)
        XCTAssertEqual(checker.destinationIdentifier(for: "/@:alice"), .landingPages)
        XCTAssertEqual(checker.destinationIdentifier(for: "/user/alice/landing"), .landingPages)
        XCTAssertEqual(checker.destinationIdentifier(for: "/communities/example/posts/create"), .postCompose)
        XCTAssertEqual(checker.destinationIdentifier(for: "/articles/create"), .postCompose)
        XCTAssertEqual(checker.destinationIdentifier(for: "/blog/create"), .postCompose)
    }

    func testCommunityActionRoutesResolveBeforeGenericCommunitySlugRoutes() throws {
        let create = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/communities/create"))
        XCTAssertEqual(create.entry.destinationIdentifier, .communityDetail)
        XCTAssertEqual(create.match.template, "/communities/create")

        let invite = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/communities/invite/invite-code"))
        XCTAssertEqual(invite.entry.destinationIdentifier, .communityDetail)
        XCTAssertEqual(invite.match.template, "/communities/invite/:code")

        let apply = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/communities/example/apply"))
        XCTAssertEqual(apply.entry.destinationIdentifier, .communityDetail)
        XCTAssertEqual(apply.match.template, "/communities/:slug/apply")

        let expectedTopLevelTemplates = [
            "/communities/:slug/about",
            "/communities/:slug/posts/pending",
            "/communities/:slug/pinned-posts",
            "/communities/:slug/applications",
            "/communities/:slug/invites",
            "/communities/:slug/bans",
            "/communities/:slug/restrictions",
            "/communities/:slug/modlog",
            "/communities/:slug/modmail",
            "/communities/:slug/modmail/:threadId",
            "/communities/:slug/moderator-stats",
            "/communities/:slug/moderator-vacation",
            "/communities/:slug/moderation-queue",
            "/communities/:slug/moderation-analytics",
            "/communities/:slug/ai-agents",
            "/communities/:slug/agent-prompts",
            "/communities/:slug/automod/**"
        ]

        for template in expectedTopLevelTemplates {
            let path = template
                .replacingOccurrences(of: ":slug", with: "example")
                .replacingOccurrences(of: ":threadId", with: "thread-1")
                .replacingOccurrences(of: "**", with: "simulate")
            let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
            XCTAssertEqual(match.entry.destinationIdentifier, .communityDetail)
            XCTAssertEqual(match.match.template, template)
        }

        let settingsPinnedPosts = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/communities/example/settings/pinned-posts")
        )
        XCTAssertEqual(settingsPinnedPosts.entry.destinationIdentifier, .communityDetail)
        XCTAssertEqual(settingsPinnedPosts.match.template, "/communities/:slug/settings/pinned-posts")
    }

    func testMatchingRoutePreservesPathParameters() throws {
        let review = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/review/native-review"))
        XCTAssertEqual(review.entry.destinationIdentifier, .postDetail)
        XCTAssertEqual(review.match.param("id"), "native-review")

        let users = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/users?q=alice"))
        XCTAssertEqual(users.entry.destinationIdentifier, .usersBrowse)
        XCTAssertEqual(users.match.queryValue("q"), "alice")

        let topic = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/swift/posts"))
        XCTAssertEqual(topic.entry.destinationIdentifier, .topicDetail)
        XCTAssertEqual(topic.match.param("idOrSlug"), "swift")

        let comparison = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/compare/apples-vs-oranges"))
        XCTAssertEqual(comparison.entry.destinationIdentifier, .compare)
        XCTAssertEqual(comparison.match.param("slugA"), "apples")
        XCTAssertEqual(comparison.match.param("slugB"), "oranges")

        let message = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/conversation-1"))
        XCTAssertEqual(message.entry.destinationIdentifier, .messages)
        XCTAssertEqual(message.match.param("conversationId"), "conversation-1")
    }

    func testIncludedCatalogEntriesCoverEveryDestinationIdentifier() {
        let includedDestinationIdentifiers = Set(NativeRouteCatalog.includedEntries.compactMap(\.destinationIdentifier))

        XCTAssertEqual(Set(NativeRouteDestinationIdentifier.allCases), includedDestinationIdentifiers)
        XCTAssertTrue(NativeRouteCatalog.excludedEntries.allSatisfy { $0.destinationIdentifier == nil })
    }

    func testDestinationIdentifierIdsMatchRawValues() {
        for destination in NativeRouteDestinationIdentifier.allCases {
            XCTAssertEqual(destination.id, destination.rawValue)
        }
    }

    func testExcludedAdminAndModeratorPatternsAreExplicit() {
        let checker = NativeRouteParityChecker()

        XCTAssertNil(checker.destinationIdentifier(for: "/admin/users"))
        XCTAssertNil(checker.destinationIdentifier(for: "/vote-integrity/audit"))
        XCTAssertNil(checker.destinationIdentifier(for: "/report-integrity"))
        XCTAssertEqual(checker.destinationIdentifier(for: "/topics/create"), .topicManagement)
        XCTAssertEqual(checker.destinationIdentifier(for: "/topic/topic-123/settings/about"), .topicManagement)
        XCTAssertEqual(checker.destinationIdentifier(for: "/communities/example/settings/moderation"), .communityDetail)
        XCTAssertEqual(checker.destinationIdentifier(for: "/discussion/post-1/tags/topic"), .postDetail)
    }

    func testCatalogEntryAccessorsAndMisses() throws {
        let included = try XCTUnwrap(NativeRouteCatalog.includedEntries.first)
        XCTAssertNotNil(included.destinationIdentifier)
        XCTAssertNil(included.exclusionReason)
        XCTAssertFalse(included.matches("/definitely-not-a-native-route"))

        let excluded = try XCTUnwrap(NativeRouteCatalog.excludedEntries.first)
        XCTAssertNil(excluded.destinationIdentifier)
        XCTAssertNotNil(excluded.exclusionReason)
        XCTAssertNil(NativeRouteCatalog.matchingEntry(for: "/definitely-not-a-native-route"))
    }
}
