import Foundation
@testable import VouchaAuth
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class CommunityViewModelCoverageTests: NativeRouteSurfaceViewModelTestCase {
    private var provider: CoverageCommunityAppAttestProvider!
    private var keyStore: AppAttestKeyStore!

    override func setUp() {
        super.setUp()
        provider = CoverageCommunityAppAttestProvider()
        keyStore = AppAttestKeyStore(
            serviceName: "ai.voucha.test.community.coverage.\(UUID().uuidString)"
        )
        CannedFeedURLProtocol.handlers["/api/v1/app-attestation/challenge"] =
            (Data(#"{"challengeId":"chal-1","challenge":"nonce-abc"}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/app-attestation/attest"] = (Data("{}".utf8), 200)
    }

    override func tearDown() {
        keyStore.clear()
        super.tearDown()
    }

    func testBrowseLoadWithoutClientAndSearchErrorBranches() async throws {
        let signedOut = CommunityBrowseViewModel(client: nil)
        signedOut.query = "native"

        await signedOut.load()

        XCTAssertEqual(signedOut.rows, [])
        XCTAssertEqual(signedOut.state, .loaded)

        CannedFeedURLProtocol.handlers["/api/v1/communities"] = (Data("{}".utf8), 500)
        let failing = try CommunityBrowseViewModel(client: makeClient())

        await failing.search(query: "broken")

        XCTAssertEqual(failing.state, .error(UiMessage(.nativeSwiftCommunityStatusUnableToLoadCommunities)))
    }

    func testCreateWithoutClientAndFallbackErrors() async throws {
        let noClient = CommunityCreateViewModel(client: nil, appAttestationService: nil)
        noClient.name = "Native Builders"
        noClient.turnstileToken = "token"

        await noClient.create()

        XCTAssertEqual(noClient.state, .loaded)

        CannedFeedURLProtocol.handlers["/api/v1/communities"] = (
            Data(#"{"code":"BYPASS_DISABLED"}"#.utf8), 403
        )
        let fallbackRequired = try makeCreateViewModel()
        fallbackRequired.name = "Native Builders"

        await fallbackRequired.create()

        XCTAssertEqual(fallbackRequired.state, .requiredTurnstile)

        CannedFeedURLProtocol.handlers["/api/v1/communities"] = (Data("{}".utf8), 500)
        provider.isSupported = false
        let failing = try makeCreateViewModel()
        failing.name = "Native Builders"
        failing.turnstileToken = "token"

        await failing.create()

        XCTAssertNil(failing.turnstileToken)
        XCTAssertEqual(failing.state, .error(UiMessage(.nativeSwiftCommunityStatusUnableToCreateCommunity)))
    }

    func testCreateUsesExistingTurnstileTokenBeforeAppAttest() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/communities"] = [
            (makeCommunityResponse(), 201, 0)
        ]
        let viewModel = try makeCreateViewModel()
        viewModel.name = "Native Builders"
        viewModel.turnstileToken = "token"

        await viewModel.create()

        XCTAssertEqual(viewModel.createdCommunitySlug, "native-builders")
        XCTAssertNil(viewModel.turnstileToken)
        XCTAssertEqual(viewModel.state, .loaded)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.allSatisfy { $0.path == "/api/v1/communities" })
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedBodies.compactMap(\.self)
                .filter { $0.contains("cf_turnstile_response") }
                .count,
            1
        )
    }

    func testDetailLoadWithoutClientAndActionErrorBranches() async throws {
        let signedOut = CommunityDetailViewModel(client: nil, slug: "builders")

        await signedOut.load()
        await signedOut.join()

        XCTAssertEqual(signedOut.state, .loaded)

        CannedFeedURLProtocol.handlers["/api/v1/communities/builders"] = (Data("{}".utf8), 500)
        let failingLoad = try CommunityDetailViewModel(client: makeClient(), slug: "builders")

        await failingLoad.load()

        XCTAssertEqual(failingLoad.state, .error(UiMessage(.nativeSwiftCommunityStatusUnableToLoadCommunity)))

        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/members"] = (Data("{}".utf8), 500)
        let failingAction = try CommunityDetailViewModel(client: makeClient(), slug: "builders")

        await failingAction.join()

        XCTAssertEqual(failingAction.state, .error(UiMessage(.nativeSwiftCommunityStatusActionFailed)))
    }

    func testDetailLoadClampsHiddenTabsAndKeepsPublicSections() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders"] = (
            Data(
                """
                {
                  "community": {
                    "id": "community-1",
                    "name": "Native Builders",
                    "slug": "builders",
                    "markdown": "SwiftUI community",
                    "visibility": "public",
                    "member_roster_visibility": "public",
                    "list_type": null,
                    "member_invites_allowed_at": null,
                    "post_approval_required_at": null,
                    "allow_review_posts": true,
                    "allow_data_point_posts": true,
                    "trusted_at": null,
                    "profile_image_id": null,
                    "banner_image_id": null,
                    "created_by_id": "user-1",
                    "created_at": "2026-01-01T00:00:00Z",
                    "updated_at": "2026-01-01T00:00:00Z",
                    "deleted_at": null,
                    "deleted_by_id": null,
                    "archived_at": null,
                    "archived_by_id": null,
                    "default_language": null,
                    "lingua_rs_detected_language": null,
                    "rules_markdown": null
                  },
                  "community_metrics": {
                    "id": "community-1",
                    "member_count": 7,
                    "post_count": 2,
                    "list_item_count": 1,
                    "proxy_follow_count": 0,
                    "proxy_mute_count": 0,
                    "virtual_subscription_count": 0
                  },
                  "membership": {
                    "id": "membership-1",
                    "community_id": "community-1",
                    "user_id": "user-1",
                    "role": "member",
                    "approved_by_id": null,
                    "created_at": "2026-01-01T00:00:00Z",
                    "updated_at": "2026-01-01T00:00:00Z",
                    "removed_at": null,
                    "removed_by_id": null
                  },
                  "has_pending_application": false
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/list-items/counts"] = (
            Data(#"{"topic":1,"rss_feed":0,"post":0,"url_hostname":0,"url":0}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts"] = (
            Data(
                #"{"results":[{"id":"post-1"}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null},"posts":{"post-1":{"id":"post-1","post_type":"discussion","title":"Native post","slug":"native-post","markdown":"Body","created_by_id":"user-1","created_at":"2026-01-01T00:00:00Z"}},"posts_metrics":{},"communities":{}}"#
                    .utf8
            ),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .settings
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedTab, .posts)
        XCTAssertTrue(viewModel.visibleTabs.contains(.news))
        XCTAssertTrue(viewModel.visibleTabs.contains(.listTopics))
        XCTAssertFalse(viewModel.visibleTabs.contains(.settings))
        XCTAssertFalse(viewModel.visibleTabs.contains(.modlog))
        XCTAssertEqual(
            viewModel.summary.rows.first,
            verbatimRow(icon: "doc.text", title: "Native post", detail: "Discussion")
        )
    }

    func testDetailLoadKeepsPinnedPostsAvailableToPublicMembersAndSkipsInviteListFetch() async throws {
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders"] = (
            Data(
                """
                {
                  "community": {
                    "id": "community-1",
                    "name": "Native Builders",
                    "slug": "builders",
                    "markdown": "SwiftUI community",
                    "visibility": "public",
                    "member_roster_visibility": "public",
                    "list_type": null,
                    "member_invites_allowed_at": "2026-07-01T00:00:00Z",
                    "post_approval_required_at": null,
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
                    "default_language": null,
                    "lingua_rs_detected_language": null,
                    "rules_markdown": null
                  },
                  "community_metrics": {
                    "id": "community-1",
                    "member_count": 7,
                    "post_count": 2,
                    "list_item_count": 1,
                    "proxy_follow_count": 0,
                    "proxy_mute_count": 0,
                    "virtual_subscription_count": 0
                  },
                  "membership": {
                    "id": "membership-1",
                    "community_id": "community-1",
                    "user_id": "user-1",
                    "role": "member",
                    "approved_by_id": null,
                    "created_at": "2026-07-01T00:00:00Z",
                    "updated_at": "2026-07-01T00:00:00Z",
                    "removed_at": null,
                    "removed_by_id": null
                  },
                  "has_pending_application": false
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/list-items/counts"] = (
            Data(#"{"topic":0,"rss_feed":0,"post":0,"url_hostname":0,"url":0}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/pinned-posts"] = (
            Data(
                #"{"pinned_posts":[{"community_id":"community-1","post_id":"post-1","order_index":0,"pinned_by_id":"user-1","created_at":"2026-07-01T00:00:00Z"}]}"#
                    .utf8
            ),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .pinnedPosts
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedTab, .pinnedPosts)
        XCTAssertTrue(viewModel.visibleTabs.contains(.pinnedPosts))
        XCTAssertTrue(viewModel.visibleTabs.contains(.invites))
        XCTAssertEqual(
            viewModel.summary.rows,
            [
                verbatimRow(
                    icon: "pin",
                    title: "Pinned post post-1",
                    detail: "Order 1"
                )
            ]
        )

        viewModel.selectedTab = .invites
        let inviteRows = try await viewModel.loadRows(client: makeClient())

        XCTAssertEqual(inviteRows, [])
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/communities/builders/invites"
        })
    }

    func testDetailLoadSkipsHiddenResourcesForPrivateNonMembers() async throws {
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.handlers["/api/v1/communities/private-builders"] = (
            Data(
                """
                {
                  "community": {
                    "id": "community-1",
                    "name": "Private Builders",
                    "slug": "private-builders",
                    "markdown": "Apply to join",
                    "visibility": "private",
                    "member_roster_visibility": "members",
                    "list_type": null,
                    "member_invites_allowed_at": null,
                    "post_approval_required_at": null,
                    "allow_review_posts": true,
                    "allow_data_point_posts": true,
                    "trusted_at": null,
                    "profile_image_id": null,
                    "banner_image_id": null,
                    "created_by_id": "user-1",
                    "created_at": "2026-01-01T00:00:00Z",
                    "updated_at": "2026-01-01T00:00:00Z",
                    "deleted_at": null,
                    "deleted_by_id": null,
                    "archived_at": null,
                    "archived_by_id": null,
                    "default_language": null,
                    "lingua_rs_detected_language": null,
                    "rules_markdown": null
                  },
                  "community_metrics": {
                    "id": "community-1",
                    "member_count": 7,
                    "post_count": 2,
                    "list_item_count": 1,
                    "proxy_follow_count": 0,
                    "proxy_mute_count": 0,
                    "virtual_subscription_count": 0
                  },
                  "membership": null,
                  "has_pending_application": false
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/private-builders/list-items/counts"] = (
            Data(#"{"error":"forbidden"}"#.utf8),
            403
        )
        let viewModel = try CommunityDetailViewModel(client: makeClient(), slug: "private-builders")

        await viewModel.load()

        XCTAssertEqual(viewModel.state, .loaded)
        XCTAssertEqual(viewModel.visibleTabs, [])
        XCTAssertEqual(viewModel.summary.rows, [])
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/communities/private-builders/list-items/counts"
        })
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/communities/private-builders/posts"
        })
    }

    private func makeCreateViewModel() throws -> CommunityCreateViewModel {
        let client = try makeClient()
        let service = AppAttestationService(client: client, provider: provider, keyStore: keyStore)
        return CommunityCreateViewModel(client: client, appAttestationService: service)
    }

    private func makeCommunityResponse() -> Data {
        Data(
            """
            {
              "community": {
                "id": "community-1",
                "name": "Native Builders",
                "slug": "native-builders",
                "markdown": "SwiftUI community",
                "visibility": "public",
                "member_roster_visibility": "public",
                "list_type": null,
                "member_invites_allowed_at": null,
                "post_approval_required_at": null,
                "allow_review_posts": true,
                "allow_data_point_posts": true,
                "trusted_at": null,
                "profile_image_id": null,
                "banner_image_id": null,
                "created_by_id": "user-1",
                "created_at": "2026-01-01T00:00:00Z",
                "updated_at": "2026-01-01T00:00:00Z",
                "deleted_at": null,
                "deleted_by_id": null,
                "archived_at": null,
                "archived_by_id": null,
                "default_language": null,
                "lingua_rs_detected_language": null,
                "rules_markdown": null
              },
              "community_metrics": {
                "id": "community-1",
                "member_count": 1,
                "post_count": 0,
                "list_item_count": 0,
                "proxy_follow_count": 0,
                "proxy_mute_count": 0,
                "virtual_subscription_count": 0
              },
              "membership": null,
              "has_pending_application": false
            }
            """.utf8
        )
    }
}

private final class CoverageCommunityAppAttestProvider: AppAttestProviding, @unchecked Sendable {
    var isSupported = true
    var generateKeyResult: Result<String, Error> = .success("stub-key-id")
    var attestKeyResult: Result<Data, Error> = .success(Data("attestation-blob".utf8))
    var generateAssertionResult: Result<Data, Error> = .success(Data("assertion-blob".utf8))

    func generateKey() async throws -> String {
        try generateKeyResult.get()
    }

    func attestKey(_: String, clientDataHash _: Data) async throws -> Data {
        try attestKeyResult.get()
    }

    func generateAssertion(_: String, clientDataHash _: Data) async throws -> Data {
        try generateAssertionResult.get()
    }
}
