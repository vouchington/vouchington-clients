import Foundation
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunitySurfaceSectionTests: NativeRouteSurfaceViewModelTestCase {
    func testDetailLoadBuildsVisibleTabsAndSettingsRows() async throws {
        registerDetailRoleFixtures()
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderator-vacation"] = (
            ApiFixtureLoader.data("web.communities.moderator-vacation.default"),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .settings
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedTab, .settings)
        XCTAssertTrue(viewModel.visibleTabs.contains(.settings))
        XCTAssertTrue(viewModel.visibleTabs.contains(.modlog))
        XCTAssertTrue(viewModel.visibleTabs.contains(.moderationAnalytics))
        XCTAssertTrue(viewModel.visibleTabs.contains(.listTopics))
        XCTAssertEqual(viewModel.summary.rows.prefix(4), [
            verbatimRow(icon: "gearshape", title: "Visibility", detail: "Public"),
            verbatimRow(icon: "person.3", title: "Roster", detail: "Public"),
            verbatimRow(icon: "checkmark.shield", title: "Post approval", detail: "Required"),
            verbatimRow(icon: "envelope", title: "Invites", detail: "Enabled")
        ])
        XCTAssertEqual(
            Set(CannedFeedURLProtocol.capturedURLs.map(\.path)),
            Set([
                "/api/v1/communities/builders",
                "/api/v1/communities/builders/list-items/counts",
                "/api/v1/communities/builders/moderator-vacation"
            ])
        )
    }

    func testSiteModeratorLoadsRawAnalyticsWithoutCommunityMembership() async throws {
        registerDetailRoleFixtures(role: nil, visibility: "private")
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderation-analytics"] = (
            ApiFixtureLoader.data("web.communities.moderation-analytics.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderation-transparency"] = (
            Data("{}".utf8),
            500
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderationAnalytics,
            isSiteModerator: true
        )
        await viewModel.load()

        XCTAssertTrue(viewModel.canViewRawModerationAnalytics)
        XCTAssertTrue(viewModel.visibleTabs.contains(.moderationAnalytics))
        XCTAssertFalse(viewModel.summary.rows.isEmpty)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/communities/builders/moderation-analytics"
        })
    }

    func testModeratorCanManageInvitesWhenMemberInvitesAreDisabled() async throws {
        registerDetailRoleFixtures(role: "moderator", memberInvitesAllowedAt: nil)
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/invites"] = (
            Data(
                """
                {
                  "results": [],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "community_invites": {}
                }
                """.utf8
            ),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .invites
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedTab, .invites)
        XCTAssertTrue(viewModel.visibleTabs.contains(.invites))
    }

    func testModeratorMemberControlsHideOwnerOnlyActions() async throws {
        registerDetailRoleFixtures(role: "moderator")
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .settings
        )

        await viewModel.load()
        viewModel.selectedTab = .members

        let panel = CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: true, showSignIn: {})
        XCTAssertNoThrow(try panel.inspect().find(button: "Remove"))
        XCTAssertThrowsError(try panel.inspect().find(button: "Update role"))
        XCTAssertThrowsError(try panel.inspect().find(button: "Transfer ownership"))
    }

    func testMemberModmailThreadRouteStaysVisible() async throws {
        registerDetailRoleFixtures(role: "member")
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/modmail/thread-1/messages"] = (
            Data(
                """
                {
                  "results": [
                    {
                      "id": "message-1",
                      "conversation_id": "thread-1",
                      "body_text": "Please review",
                      "created_by_id": "user-1",
                      "sender_username": "member",
                      "created_at": "2026-07-01T00:00:00Z",
                      "updated_at": null,
                      "deleted_at": null
                    }
                  ],
                  "page_info": { "has_next_page": false, "end_cursor": null }
                }
                """.utf8
            ),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .modmail,
            modmailThreadId: "thread-1"
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedTab, .modmail)
        XCTAssertTrue(viewModel.visibleTabs.contains(.modmail))
        XCTAssertEqual(viewModel.summary.rows, [
            verbatimRow(
                icon: "bubble.left.and.text.bubble.right",
                title: "@member",
                detail: "Please review"
            )
        ])
    }

    func testAdministratorCanLoadPrivateCommunityRowsWithoutMembership() async throws {
        registerDetailRoleFixtures(role: nil, visibility: "private")
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts"] = (
            Data(
                """
                {
                  "results": [{ "id": "post-1" }],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "posts": {
                    "post-1": {
                      "id": "post-1",
                      "post_type": "discussion",
                      "title": "Private admin post",
                      "slug": "private-admin-post",
                      "markdown": "Body",
                      "created_by_id": "user-1",
                      "created_at": "2026-07-01T00:00:00Z"
                    }
                  },
                  "post_link_embeds": {
                    "post-1": {
                      "source_url": "https://example.com/private-admin-post",
                      "title": "Private admin preview",
                      "thumbnail_url": "https://cdn.example.com/private-admin.jpg"
                    }
                  },
                  "posts_metrics": {},
                  "communities": {}
                }
                """.utf8
            ),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            isAdministrator: true
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedTab, .posts)
        XCTAssertTrue(viewModel.visibleTabs.contains(.posts))
        XCTAssertEqual(viewModel.summary.rows.first, verbatimRow(
            icon: "doc.text",
            title: "Private admin post",
            detail: "Discussion"
        ))
        XCTAssertEqual(viewModel.postEmbedsByPostId["post-1"]?.previewTitle, "Private admin preview")
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/communities/builders/list-items/counts"
        })
    }

    func testAdministratorCanSeeModmailWithoutMembership() async throws {
        registerDetailRoleFixtures(role: nil, visibility: "private")
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/modmail"] = (
            Data(
                """
                {
                  "results": [],
                  "page_info": { "has_next_page": false, "end_cursor": null }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts"] = (
            Data(
                """
                {
                  "results": [{ "id": "post-1" }],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "posts": {
                    "post-1": {
                      "id": "post-1",
                      "post_type": "discussion",
                      "title": "Fallback post",
                      "slug": "fallback-post",
                      "markdown": "Body",
                      "created_by_id": "user-1",
                      "created_at": "2026-07-01T00:00:00Z"
                    }
                  },
                  "posts_metrics": {},
                  "communities": {}
                }
                """.utf8
            ),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .modmail,
            isAdministrator: true
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedTab, .modmail)
        XCTAssertTrue(viewModel.visibleTabs.contains(.modmail))
        XCTAssertEqual(viewModel.summary.rows, [])
    }

    func testAdministratorWithoutMembershipDoesNotHydrateModeratorVacation() async throws {
        registerDetailRoleFixtures(role: nil)
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts/pending"] = (
            Data(
                """
                {
                  "results": [],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "posts": {},
                  "posts_metrics": {},
                  "communities": {}
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/reports/pending"] = (
            Data(
                #"{"reports":[],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}"#.utf8
            ),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation,
            isAdministrator: true
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.state, .loaded)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/communities/builders/moderator-vacation"
        })
    }

    func testAdministratorWithoutMembershipCannotSelectModeratorVacation() async throws {
        registerDetailRoleFixtures(role: nil)
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts"] = (
            Data(
                #"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null},"posts":{},"posts_metrics":{},"communities":{}}"#
                    .utf8
            ),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderatorVacation,
            isAdministrator: true
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.state, .loaded)
        XCTAssertNotEqual(viewModel.selectedTab, .moderatorVacation)
        XCTAssertFalse(viewModel.visibleTabs.contains(.moderatorVacation))
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/communities/builders/moderator-vacation"
        })
    }

    func testOwnerCanSelectAndLoadModeratorVacation() async throws {
        try await assertCommunityMemberCanSelectModeratorVacation(role: "owner")
    }

    func testModeratorCanSelectAndLoadModeratorVacation() async throws {
        try await assertCommunityMemberCanSelectModeratorVacation(role: "moderator")
    }

    func testSiteModeratorCanSeeStaffModmailWithoutCommunityManagement() async throws {
        registerDetailRoleFixtures(role: nil, visibility: "private")
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/modmail"] = (
            Data(
                """
                {
                  "results": [],
                  "page_info": { "has_next_page": false, "end_cursor": null }
                }
                """.utf8
            ),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .modmail,
            isSiteModerator: true
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedTab, .modmail)
        XCTAssertTrue(viewModel.visibleTabs.contains(.modmail))
        XCTAssertTrue(viewModel.visibleTabs.contains(.modlog))
        XCTAssertFalse(viewModel.visibleTabs.contains(.moderatorVacation))
        XCTAssertFalse(viewModel.visibleTabs.contains(.bans))
        XCTAssertFalse(viewModel.visibleTabs.contains(.restrictions))
        XCTAssertFalse(viewModel.canModerateCommunity)
        XCTAssertEqual(
            Set(CannedFeedURLProtocol.capturedURLs.map(\.path)),
            Set(["/api/v1/communities/builders", "/api/v1/communities/builders/modmail"])
        )
    }

    func testRegularMemberLoadsOnlyPaidCommunityTransparency() async throws {
        registerDetailRoleFixtures(role: "member")
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderation-transparency"] = (
            Data("""
            {"range":"30d","buckets":[{"date":"2026-08-01","metric":"reports","category":"spam","count":20}]}
            """.utf8),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderationAnalytics
        )
        await viewModel.load()

        XCTAssertEqual(viewModel.selectedTab, .moderationAnalytics)
        XCTAssertTrue(viewModel.visibleTabs.contains(.moderationAnalytics))
        XCTAssertEqual(viewModel.summary.rows, [
            verbatimRow(
                icon: "shield",
                title: "Reports · Spam",
                detail: "Released Aug 1, 2026 · Spam · 20"
            )
        ])
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/communities/builders/moderation-transparency"
        })
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/communities/builders/moderation-analytics"
        })
    }

    private func registerDetailRoleFixtures() {
        registerDetailRoleFixtures(role: "owner")
    }

    private func assertCommunityMemberCanSelectModeratorVacation(role: String) async throws {
        registerDetailRoleFixtures(role: role)
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderator-vacation"] = (
            ApiFixtureLoader.data("web.communities.moderator-vacation.default"),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderatorVacation
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedTab, .moderatorVacation)
        XCTAssertTrue(viewModel.visibleTabs.contains(.moderatorVacation))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/communities/builders/moderator-vacation"
        })
    }

    private func registerDetailRoleFixtures(
        role: String?,
        visibility: String = "public",
        memberInvitesAllowedAt: String? = "2026-07-01T00:00:00Z"
    ) {
        let memberInvitesAllowedAtJSON = memberInvitesAllowedAt.map { #""\#($0)""# } ?? "null"
        let membershipJSON = if let role {
            """
                  "membership": {
                    "id": "membership-1",
                    "community_id": "community-1",
                    "user_id": "user-1",
                    "role": "\(role)",
                    "approved_by_id": null,
                    "created_at": "2026-07-01T00:00:00Z",
                    "updated_at": "2026-07-01T00:00:00Z",
                    "removed_at": null,
                    "removed_by_id": null
                  },
            """
        } else {
            #"                  "membership": null,"#
        }
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders"] = (
            Data(
                """
                {
                  "community": {
                    "id": "community-1",
                    "name": "Native Builders",
                    "slug": "builders",
                    "markdown": "SwiftUI community",
                    "visibility": "\(visibility)",
                    "member_roster_visibility": "public",
                    "list_type": "follow",
                    "member_invites_allowed_at": \(memberInvitesAllowedAtJSON),
                    "post_approval_required_at": "2026-07-01T00:00:00Z",
                    "allow_review_posts": true,
                    "allow_data_point_posts": true,
                    "trusted_at": null,
                    "profile_image_id": null,
                    "banner_image_id": null,
                    "created_by_id": "user-1",
                    "created_at": "2026-07-01T00:00:00Z",
                    "updated_at": "2026-07-01T00:00:00Z",
                    "deleted_at": null,
                    "deleted_by_id": null,
                    "archived_at": null,
                    "archived_by_id": null,
                    "default_language": "en",
                    "lingua_rs_detected_language": null,
                    "rules_markdown": "Be kind."
                  },
                  "community_metrics": {
                    "id": "community-1",
                    "member_count": 7,
                    "post_count": 2,
                    "list_item_count": 5,
                    "proxy_follow_count": 0,
                    "proxy_mute_count": 0,
                    "virtual_subscription_count": 0
                  },
                \(membershipJSON)
                  "has_pending_application": false
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/list-items/counts"] = (
            Data(#"{"topic":1,"rss_feed":2,"post":3,"url_hostname":4,"url":5}"#.utf8),
            200
        )
    }
}
