import Foundation
@testable import VouchaAuth
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class CommunityViewModelTests: NativeRouteSurfaceViewModelTestCase {
    private var provider: StubCommunityAppAttestProvider!
    private var keyStore: AppAttestKeyStore!

    override func setUp() {
        super.setUp()
        provider = StubCommunityAppAttestProvider()
        keyStore = AppAttestKeyStore(serviceName: "ai.voucha.test.community.\(UUID().uuidString)")
        CannedFeedURLProtocol.handlers["/api/v1/app-attestation/challenge"] =
            (Data(#"{"challengeId":"chal-1","challenge":"nonce-abc"}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/app-attestation/attest"] = (Data("{}".utf8), 200)
    }

    override func tearDown() {
        keyStore.clear()
        super.tearDown()
    }

    func testBrowseSearchLoadsCommunities() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities"] = (
            Data(
                """
                {
                  "results": [{ "id": "community-1" }],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "communities": {
                    "community-1": {
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
                    }
                  },
                  "community_metrics": {
                    "community-1": {
                      "id": "community-1",
                      "member_count": 7,
                      "post_count": 2,
                      "list_item_count": 3,
                      "proxy_follow_count": 0,
                      "proxy_mute_count": 0,
                      "virtual_subscription_count": 0
                    }
                  }
                }
                """.utf8
            ),
            200
        )
        let viewModel = try CommunityBrowseViewModel(client: makeClient())

        await viewModel.search(query: "native")

        XCTAssertEqual(viewModel.rows, [
            CommunityBrowseItem(
                id: "community-1",
                title: "Native Builders",
                detail: "SwiftUI community",
                metrics: .count(7, item: "member")
            )
        ])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "limit=25&q=native")
    }

    func testCreateUsesAppAttestHeadersInsteadOfTurnstile() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities"] = (makeCommunityResponse(), 201)
        let viewModel = try makeCreateViewModel()
        viewModel.name = "Native Builders Community"
        viewModel.slug = "native-builders"
        viewModel.markdown = "SwiftUI community"

        XCTAssertTrue(viewModel.canCreate)

        await viewModel.create()

        XCTAssertEqual(viewModel.createdCommunitySlug, "native-builders")
        XCTAssertNil(viewModel.turnstileToken)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/communities" })
        XCTAssertFalse(CannedFeedURLProtocol.capturedBodies.compactMap(\.self)
            .contains { $0.contains("cf_turnstile_response") })
        XCTAssertEqual(viewModel.state, .loaded)
    }

    func testCreateRequiresTurnstileWhenAppAttestIsUnsupported() async throws {
        provider.isSupported = false
        let viewModel = try makeCreateViewModel()
        viewModel.name = "Native Builders Community"

        XCTAssertFalse(viewModel.canCreate)

        await viewModel.create()

        XCTAssertEqual(viewModel.state, .requiredTurnstile)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testDetailActionsCallCommunityEndpoints() async throws {
        registerDetailFixtures()
        let viewModel = try CommunityDetailViewModel(client: makeClient(), slug: "builders")

        await viewModel.load()
        await viewModel.join()
        await viewModel.leave()
        await viewModel.archive()
        await viewModel.unarchive()
        viewModel.selectedTab = .applications
        viewModel.applicationMessage = "Let me in"
        await viewModel.apply()
        viewModel.selectedTab = .invites
        viewModel.inviteRecipient = "alice"
        await viewModel.invite()

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedMethods.filter { $0 == "POST" }.count,
            3
        )
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/communities/builders/members" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/applications" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/communities/builders/invites" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""archive":true"#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""archive":false"#) == true })
    }

    func testApplicationQuestionsRequireAnswersBeforeApply() async throws {
        registerDetailFixtures()
        CannedFeedURLProtocol.queuedHandlers["/api/v1/communities/builders/applications"] = [
            (makeApplicationsResponse(), 200, 0),
            (Data("{}".utf8), 200, 0),
            (makeApplicationsResponse(), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/application-questions"] = (
            makeApplicationQuestionsResponse(),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .applications,
            isApplicationFormRoute: true
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.applicationQuestions.map(\.id), ["question-1"])

        let requestCountBeforeMissingAnswer = CannedFeedURLProtocol.capturedURLs.count
        await viewModel.apply()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, requestCountBeforeMissingAnswer)
        XCTAssertEqual(
            viewModel.state,
            .error(UiMessage(.nativeSwiftCommunityStatusApplicationAnswersMissing))
        )

        viewModel.applicationAnswers["question-1"] = .string("Because SwiftUI")
        viewModel.applicationMessage = "Let me in"
        await viewModel.apply()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/applications" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""question-1":"Because SwiftUI""#) == true })
        XCTAssertTrue(viewModel.applicationAnswers.isEmpty)
        XCTAssertEqual(viewModel.applicationMessage, "")
    }

    func testApplyRouteLoadsQuestionsWithoutPrivateDetailOrModeratorRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/application-questions"] = (
            makeApplicationQuestionsResponse(),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .applications,
            isApplicationFormRoute: true
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.applicationQuestions.map(\.id), ["question-1"])
        XCTAssertEqual(viewModel.summary.rows, [])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.map(\.path),
            ["/api/v1/communities/builders/application-questions"]
        )
    }

    func testApplicationAnswersKeepCheckboxAndMultiSelectTypes() async throws {
        registerDetailFixtures()
        CannedFeedURLProtocol.queuedHandlers["/api/v1/communities/builders/applications"] = [
            (makeApplicationsResponse(), 200, 0),
            (Data("{}".utf8), 200, 0),
            (makeApplicationsResponse(), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/application-questions"] = (
            Data(
                """
                {
                  "questions": [
                    {
                      "id": "question-checkbox",
                      "community_id": "community-1",
                      "question": "Agree?",
                      "field_type": "checkbox",
                      "options": null,
                      "order_index": 0,
                      "required": true,
                      "created_at": "2026-01-01T00:00:00Z",
                      "deleted_at": null
                    },
                    {
                      "id": "question-multi",
                      "community_id": "community-1",
                      "question": "Platforms?",
                      "field_type": "multi_select",
                      "options": ["swift", "ios"],
                      "order_index": 1,
                      "required": true,
                      "created_at": "2026-01-01T00:00:00Z",
                      "deleted_at": null
                    }
                  ]
                }
                """.utf8
            ),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .applications
        )

        await viewModel.load()
        viewModel.applicationAnswers["question-checkbox"] = .bool(true)
        viewModel.applicationAnswers["question-multi"] = .array([.string("swift"), .string("ios")])
        await viewModel.apply()

        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""question-checkbox":true"#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""question-multi":["swift","ios"]"#) == true })
    }

    func testInviteRedemptionCallsInviteRedemptionEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities/invite-redemptions"] = (Data("{}".utf8), 200)
        let viewModel = try CommunityInviteRedemptionViewModel(client: makeClient(), code: "code-123")

        await viewModel.redeem()

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/communities/invite-redemptions")
        XCTAssertEqual(CannedFeedURLProtocol.capturedBodies.first ?? nil, #"{"code":"code-123"}"#)
        XCTAssertEqual(viewModel.statusMessage, UiMessage(.nativeSwiftCommunityStatusInviteRedeemed))
        XCTAssertEqual(viewModel.state, .loaded)
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

    private func registerDetailFixtures() {
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders"] = (
            Data(
                """
                {
                  "community": {
                    "id": "community-1",
                    "name": "Builders",
                    "slug": "builders",
                    "markdown": "Native community",
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
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts"] = (
            Data(#"{"results":[],"posts":{}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/members"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/applications"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/application-questions"] = (
            Data(#"{"questions":[]}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/invites"] = (Data("{}".utf8), 200)
    }

    private func makeApplicationsResponse() -> Data {
        Data(
            """
            {
              "results": [],
              "page_info": { "has_next_page": false, "end_cursor": null },
              "community_applications": {}
            }
            """.utf8
        )
    }

    private func makeApplicationQuestionsResponse() -> Data {
        Data(
            """
            {
              "questions": [
                {
                  "id": "question-1",
                  "community_id": "community-1",
                  "question": "Why join?",
                  "field_type": "short_text",
                  "options": null,
                  "order_index": 0,
                  "required": true,
                  "created_at": "2026-01-01T00:00:00Z",
                  "updated_at": "2026-01-01T00:00:00Z",
                  "deleted_at": null
                }
              ]
            }
            """.utf8
        )
    }

}

private final class StubCommunityAppAttestProvider: AppAttestProviding, @unchecked Sendable {
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
