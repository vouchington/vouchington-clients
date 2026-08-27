import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativePostComposeViewModelTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
    }

    func testPublishCreatesDiscussionPostAndClearsDraftFields() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (makePostEnvelope(id: "post-1"), 201)
        let viewModel = try NativePostComposeViewModel(client: makeClient())
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.publish()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/posts")
        XCTAssertEqual(viewModel.publishedPostId, "post-1")
        XCTAssertEqual(viewModel.title, "")
        XCTAssertEqual(viewModel.bodyText, "")
        XCTAssertTrue(viewModel.drafts.isEmpty)
        assertState(viewModel.state, .loaded)
    }

    func testPublishRequiresTurnstileBeforeSending() async throws {
        let viewModel = try NativePostComposeViewModel(client: makeClient())
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"

        await viewModel.publish()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertEqual(viewModel.drafts.first?.title, "Native title")
        XCTAssertEqual(viewModel.drafts.first?.detail, "Native body")
        assertState(viewModel.state, .required(.turnstile))
    }

    func testReviewPublishSendsTopicRatingsAndClearsFields() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (makePostEnvelope(id: "post-2"), 201)
        let viewModel = try NativePostComposeViewModel(client: makeClient())
        viewModel.postType = .review
        viewModel.title = "Native review"
        viewModel.bodyText = "Review body"
        viewModel.reviewTopicRatings = [.init(topicId: "topic-1", rating: 4)]
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.publish()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/posts")
        XCTAssertEqual(viewModel.publishedPostId, "post-2")
        XCTAssertEqual(viewModel.title, "")
        XCTAssertEqual(viewModel.bodyText, "")
        XCTAssertEqual(viewModel.reviewTopicRatings.first?.topicId, "")
        XCTAssertEqual(viewModel.reviewTopicRatings.first?.rating, 0)
        assertState(viewModel.state, .loaded)
    }

    func testLinkPublishSendsURLAndClearsFields() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (makePostEnvelope(id: "post-3"), 201)
        let viewModel = try NativePostComposeViewModel(client: makeClient())
        viewModel.postType = .link
        viewModel.title = "Native link"
        viewModel.bodyText = "Link body"
        viewModel.linkURL = "https://example.com/native"
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.publish()

        XCTAssertEqual(viewModel.publishedPostId, "post-3")
        XCTAssertEqual(viewModel.linkURL, "")
        XCTAssertEqual(viewModel.bodyText, "")
        assertState(viewModel.state, .loaded)
    }

    func testDataPointPublishSendsStructuredDataAndClearsFields() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (makePostEnvelope(id: "post-4"), 201)
        let viewModel = try NativePostComposeViewModel(client: makeClient())
        viewModel.postType = .dataPoint
        viewModel.title = "Native data point"
        viewModel.bodyText = "Data point body"
        viewModel.dataPointVertical = .creditCard
        viewModel.dataPointStructuredDataJSON =
            #"{"vertical":"credit_card","schema_version":1,"topic_ids":["topic-1"],"result":"approved"}"#
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.publish()

        XCTAssertEqual(viewModel.publishedPostId, "post-4")
        XCTAssertNil(viewModel.dataPointVertical)
        XCTAssertEqual(viewModel.dataPointStructuredDataJSON, "")
        assertState(viewModel.state, .loaded)
    }

    func testCommunityPublishUsesCommunityPostEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities/voucha/posts"] = (makePostEnvelope(id: "post-5"), 201)
        let viewModel = try NativePostComposeViewModel(client: makeClient(), communityIdOrSlug: "voucha")
        viewModel.title = "Community post"
        viewModel.bodyText = "Community body"
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.publish()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/communities/voucha/posts")
        XCTAssertEqual(viewModel.publishedPostId, "post-5")
        assertState(viewModel.state, .loaded)
    }

    func testNoClientPublishSavesDraftWithoutNetwork() async {
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.title = "Offline title"

        await viewModel.publish()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertEqual(viewModel.drafts.first?.title, "Offline title")
        assertState(viewModel.state, .loaded)
    }

    func testStoryPublishIsNotSentThroughGenericPostEndpoint() async throws {
        let viewModel = try NativePostComposeViewModel(client: makeClient())
        viewModel.postType = .story
        viewModel.title = "Story title"
        viewModel.bodyText = "Story body"
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.publish()

        XCTAssertFalse(viewModel.canSubmit)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertNil(viewModel.publishedPostId)
        XCTAssertTrue(viewModel.drafts.isEmpty)
        assertState(viewModel.state, .idle)
    }

    func testDiscussionPostsWithImagesStillRequireCoreText() async throws {
        let viewModel = try NativePostComposeViewModel(client: makeClient())
        viewModel.images = [.init(imageId: "image-1")]

        XCTAssertFalse(viewModel.canSubmit)

        await viewModel.publish()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertTrue(viewModel.drafts.isEmpty)
        assertState(viewModel.state, .idle)
    }

    func testFailedPublishClearsTurnstileToken() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (Data(#"{"message":"try again"}"#.utf8), 500)
        let viewModel = try NativePostComposeViewModel(client: makeClient())
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"
        viewModel.turnstileToken = "spent-token"

        await viewModel.publish()

        XCTAssertNil(viewModel.turnstileToken)
        XCTAssertEqual(viewModel.drafts.first?.title, "Native title")
        assertState(viewModel.state, .error(.api(statusCode: 500, preconditionCode: nil)))
    }

    func testCanSubmitReflectsPostTypeRequirements() throws {
        let viewModel = try NativePostComposeViewModel(client: makeClient())

        viewModel.images = [NativePostComposeImageDraft(imageId: "image-only")]
        XCTAssertFalse(viewModel.canSubmit)
        viewModel.images = []

        viewModel.postType = .link
        XCTAssertFalse(viewModel.canSubmit)
        viewModel.linkURL = "https://example.com"
        XCTAssertTrue(viewModel.canSubmit)

        viewModel.postType = .review
        viewModel.title = "Review"
        viewModel.reviewTopicRatings = [.init(topicId: "topic-1", rating: 0)]
        XCTAssertFalse(viewModel.canSubmit)
        viewModel.reviewTopicRatings = [.init(topicId: "topic-1", rating: 5)]
        XCTAssertTrue(viewModel.canSubmit)

        viewModel.postType = .dataPoint
        viewModel.dataPointVertical = .creditCard
        viewModel.dataPointStructuredDataJSON = #"{"score":0.9}"#
        XCTAssertTrue(viewModel.canSubmit)

        viewModel.postType = .story
        XCTAssertFalse(viewModel.canSubmit)
    }

    func testAvailablePostTypesRespectAdminAndCommunityPolicy() throws {
        let global = try NativePostComposeViewModel(client: makeClient())
        XCTAssertEqual(global.availablePostTypes, [.discussion, .review, .dataPoint, .link])

        let admin = try NativePostComposeViewModel(client: makeClient(), isAdministrator: true)
        XCTAssertEqual(
            admin.availablePostTypes,
            [.discussion, .review, .dataPoint, .link, .article, .blogPost]
        )

        let community = try NativePostComposeViewModel(
            client: makeClient(),
            communityIdOrSlug: "community-1",
            isAdministrator: true
        )
        XCTAssertEqual(community.availablePostTypes, [.discussion, .review, .dataPoint])

        let emptyCommunity = try NativePostComposeViewModel(
            client: makeClient(),
            communityIdOrSlug: "",
            isAdministrator: true
        )
        XCTAssertEqual(
            emptyCommunity.availablePostTypes,
            [.discussion, .review, .dataPoint, .link, .article, .blogPost]
        )
    }

    func testUnsupportedPostTypesCannotSubmit() throws {
        let nonAdmin = try NativePostComposeViewModel(client: makeClient())
        nonAdmin.title = "Native article"
        nonAdmin.bodyText = "Body"
        nonAdmin.postType = .article
        XCTAssertFalse(nonAdmin.canSubmit)

        let community = try NativePostComposeViewModel(
            client: makeClient(),
            communityIdOrSlug: "community-1",
            isAdministrator: true
        )
        community.title = "Community link"
        community.bodyText = "Body"
        community.postType = .link
        XCTAssertFalse(community.canSubmit)
    }

    func testInitialPostTypeNormalizesToAvailableTypes() throws {
        let community = try NativePostComposeViewModel(
            client: makeClient(),
            communityIdOrSlug: "community-1",
            initialPostType: .link
        )
        XCTAssertEqual(community.postType, .discussion)

        let admin = try NativePostComposeViewModel(
            client: makeClient(),
            initialPostType: .blogPost,
            isAdministrator: true
        )
        XCTAssertEqual(admin.postType, .blogPost)
    }

    func testReviewTopicRatingRemovalKeepsAtLeastOneDraft() {
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.reviewTopicRatings = [.init(topicId: "topic-1", rating: 4)]

        viewModel.removeReviewTopicRating(at: 0)

        XCTAssertEqual(viewModel.reviewTopicRatings.count, 1)
        XCTAssertEqual(viewModel.reviewTopicRatings.first?.topicId, "")
        XCTAssertEqual(viewModel.reviewTopicRatings.first?.rating, 0)

        viewModel.addReviewTopicRating()
        XCTAssertEqual(viewModel.reviewTopicRatings.count, 2)
    }

    func testSaveDraftRowsReflectPostTypes() {
        let viewModel = NativePostComposeViewModel(client: nil)

        viewModel.postType = .link
        viewModel.title = "Saved link"
        viewModel.linkURL = "https://example.com/link"
        viewModel.saveDraft()
        XCTAssertEqual(
            viewModel.drafts.first,
            verbatimRow(icon: "link", title: "Saved link", detail: "https://example.com/link")
        )

        viewModel.postType = .review
        viewModel.title = "Saved review"
        viewModel.reviewTopicRatings = [.init(topicId: "topic-1", rating: 5)]
        viewModel.saveDraft()
        XCTAssertEqual(viewModel.drafts.first, verbatimRow(icon: "star", title: "Saved review", detail: "1 rating"))

        viewModel.postType = .dataPoint
        viewModel.title = "Saved data"
        viewModel.dataPointVertical = .creditCard
        viewModel.dataPointStructuredDataJSON = #"{"score":1}"#
        viewModel.saveDraft()
        XCTAssertEqual(
            viewModel.drafts.first,
            verbatimRow(icon: "chart.bar", title: "Saved data", detail: "Credit card")
        )
    }

    func testSaveDraftAndSubmitAreBlockedDuringImageUploads() {
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.title = "Native title"
        viewModel.isUploadingImages = true

        XCTAssertFalse(viewModel.canSaveDraft)
        XCTAssertFalse(viewModel.canSubmit)

        viewModel.saveDraft()

        XCTAssertTrue(viewModel.drafts.isEmpty)
        XCTAssertEqual(viewModel.imageUploadErrorMessage, .app(UiMessage(.nativeSwiftPostComposeImageWaitForDraft)))
    }

    func testPublishErrorSavesDraftAndSurfacesError() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (Data(#"{"error":"failed"}"#.utf8), 500)
        let viewModel = try NativePostComposeViewModel(client: makeClient())
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.publish()

        XCTAssertEqual(viewModel.drafts.first?.title, "Native title")
        if case .error = viewModel.state {
            return
        }
        XCTFail("Expected error state")
    }

    private func makeClient() throws -> APIClient {
        try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func makePostEnvelope(id: String) -> Data {
        Data("""
        {
          "post": {
            "id": "\(id)",
            "slug": null,
            "post_type": "discussion",
            "title": "Native title",
            "markdown": "Native body",
            "html": null,
            "parent_id": null,
            "root_id": null,
            "created_by_id": "user-1",
            "created_at": "2026-01-01T00:00:00Z",
            "broadcast": "everyone",
            "privacy": "public",
            "is_anonymous": false,
            "community_id": null,
            "clearance_status": "pending",
            "deleted_at": null,
            "deleted_by_id": null,
            "locked_at": null,
            "locked_by_id": null,
            "archived_at": null,
            "archived_by_id": null,
            "clearance_reason": null,
            "clearance_updated_at": null,
            "spam_detection_created_at": null,
            "spam_detection_flagged": null,
            "spam_detection_results": null,
            "spam_detection_score": null,
            "updated_by_id": null
          }
        }
        """.utf8)
    }

    private func assertState(
        _ state: NativePostComposeState,
        _ expected: NativePostComposeState,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        switch (state, expected) {
        case (.idle, .idle), (.loading, .loading), (.loaded, .loaded):
            return
        case (.required(.turnstile), .required(.turnstile)):
            return
        case let (.error(lhs), .error(rhs)):
            XCTAssertEqual(lhs.localizedDescription, rhs.localizedDescription, file: file, line: line)
        default:
            XCTFail("Unexpected state mismatch", file: file, line: line)
        }
    }
}
