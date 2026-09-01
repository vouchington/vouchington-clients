import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativeTopicRecommendationViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testContributionAdmissionCodesSelectLocalizedPresentationKeys() {
        XCTAssertEqual(
            NativeContributionAdmissionPresentation.messageKey("CONTRIBUTION_ADMISSION_IN_PROGRESS"),
            .nativeTaxonomyContributionAdmissionInProgress
        )
        XCTAssertEqual(
            NativeContributionAdmissionPresentation.messageKey("IDEMPOTENCY_KEY_REUSED"),
            .nativeTaxonomyContributionAdmissionIdempotencyMismatch
        )
        XCTAssertEqual(
            NativeContributionAdmissionPresentation.messageKey("CONTRIBUTION_QUOTA_EXCEEDED"),
            .nativeTaxonomyContributionAdmissionCapacityUnavailable
        )
        XCTAssertNil(NativeContributionAdmissionPresentation.messageKey("UNRELATED"))
    }

    func testCreateRequiresCoreFieldsAndTurnstile() async throws {
        let viewModel = try NativeTopicRecommendationViewModel(client: makeClient())

        XCTAssertFalse(viewModel.canSubmit)

        viewModel.topicTitle = "Native Topic"
        viewModel.topicSlug = "native-topic"
        viewModel.bodyText = "Please add this topic."

        XCTAssertTrue(viewModel.canSubmit)

        await viewModel.submit()

        if case .required = viewModel.state {} else {
            XCTFail("Expected Turnstile requirement")
        }
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testCreateSubmitsTopicRecommendation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topic-recommendations"] = (
            Data(#"{"post":{"id":"topic-rec-1"}}"#.utf8),
            201
        )
        let viewModel = try NativeTopicRecommendationViewModel(client: makeClient())
        viewModel.topicTitle = "Native Topic"
        viewModel.topicSlug = "native-topic"
        viewModel.bodyText = "Please add this topic."
        viewModel.turnstileToken = "token"

        await viewModel.submit()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/topic-recommendations")
        XCTAssertEqual(viewModel.savedPostId, "topic-rec-1")
        XCTAssertEqual(viewModel.topicTitle, "")
    }

    func testReferralAndCardRequirements() throws {
        let viewModel = try NativeTopicRecommendationViewModel(client: makeClient())
        viewModel.topicTitle = "Native Topic"
        viewModel.topicSlug = "native-topic"
        viewModel.bodyText = "Please add this topic."

        viewModel.topicType = "referral_program"
        XCTAssertFalse(viewModel.canSubmit)
        viewModel.exampleReferralLink = "https://example.com/referral"
        XCTAssertTrue(viewModel.canSubmit)

        viewModel.topicType = "card"
        XCTAssertFalse(viewModel.canSubmit)
        viewModel.landingPageUrls = "https://example.com/card"
        XCTAssertTrue(viewModel.canSubmit)
    }

    func testEditLoadsDefaultsAndPatchesRecommendation() async throws {
        CannedFeedURLProtocol.handlers = [
            "/api/v1/topic-recommendations/recommendation-1": (
                ApiFixtureLoader.data("native.topic-recommendation.detail.default"),
                200
            )
        ]
        let viewModel = try NativeTopicRecommendationViewModel(
            client: makeClient(),
            recommendationId: "recommendation-1"
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.title, "Native recommendation")
        XCTAssertEqual(viewModel.topicTitle, "Native Topic")
        XCTAssertEqual(viewModel.topicType, "topic")
        XCTAssertEqual(viewModel.bodyText, "Recommendation body")
        XCTAssertEqual(viewModel.postElection?.votesCountUp, 1)
        XCTAssertEqual(viewModel.currentVote, .support)

        CannedFeedURLProtocol.handlers["/api/v1/posts/recommendation-1/vote"] = (Data("{}".utf8), 204)
        await viewModel.vote(.oppose)
        XCTAssertEqual(viewModel.currentVote, .oppose)
        XCTAssertEqual(viewModel.postElection?.votesCountUp, 0)
        XCTAssertEqual(viewModel.postElection?.votesCountDown, 1)

        CannedFeedURLProtocol.capturedURLs = []
        await viewModel.submit()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/topic-recommendations/recommendation-1")
        XCTAssertEqual(viewModel.savedPostId, "recommendation-1")
    }

    func testVoteIgnoresConcurrentMutationWhileRequestIsInFlight() async throws {
        CannedFeedURLProtocol.handlers = [
            "/api/v1/topic-recommendations/recommendation-1": (
                ApiFixtureLoader.data("native.topic-recommendation.detail.default"),
                200
            )
        ]
        let votePath = "/api/v1/posts/recommendation-1/vote"
        CannedFeedURLProtocol.suspendResponse(path: votePath)
        let requestBarrier = CannedFeedURLProtocol.requestBarrier(path: votePath, method: "PUT")
        let viewModel = try NativeTopicRecommendationViewModel(
            client: makeClient(),
            recommendationId: "recommendation-1"
        )
        await viewModel.load()

        let first = Task { await viewModel.vote(.oppose) }
        _ = try await requestBarrier.wait()
        XCTAssertTrue(viewModel.isVoting)
        await viewModel.vote(.support)
        CannedFeedURLProtocol.releaseResponse(path: votePath)
        await first.value

        XCTAssertFalse(viewModel.isVoting)
        XCTAssertEqual(viewModel.currentVote, .oppose)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter {
            $0.path == votePath
        }.count, 1)
    }

    func testRecommendationSurfaceCarriesRestrictedNegativeVoteCountVisibility() {
        let surface = NativeTopicRecommendationSurface(
            client: nil,
            recommendationId: "topic-rec-1",
            turnstileSiteKey: "site-key",
            hideDownCount: true
        )

        XCTAssertTrue(surface.hideDownCount)
    }

    func testPublicDetailRendersRecommendationBallotWithoutEditorOrSaveAction() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topic-recommendations/recommendation-1"] = (
            ApiFixtureLoader.data("native.topic-recommendation.detail.default"),
            200
        )
        let client = try makeClient()
        let surface = NativeTopicRecommendationSurface(
            client: client,
            recommendationId: "recommendation-1",
            turnstileSiteKey: nil,
            isForm: false
        )

        try await ViewHosting.host(surface) {
            for _ in 0 ..< 20 {
                if (try? surface.inspect().find(button: "Support")) != nil {
                    break
                }
                try await Task.sleep(nanoseconds: 25_000_000)
            }
            XCTAssertNoThrow(try surface.inspect().find(text: "Native recommendation"))
            XCTAssertNoThrow(try surface.inspect().find(text: "Recommendation body"))
            XCTAssertNoThrow(try surface.inspect().find(button: "Support"))
            XCTAssertNoThrow(try surface.inspect().find(button: "Oppose"))
            XCTAssertThrowsError(try surface.inspect().find(button: "Save recommendation"))
            XCTAssertThrowsError(try surface.inspect().find(ViewType.TextField.self))
            XCTAssertThrowsError(try surface.inspect().find(ViewType.TextEditor.self))
        }

        var signInCount = 0
        let signedOutSurface = NativeTopicRecommendationSurface(
            client: client,
            recommendationId: "recommendation-1",
            turnstileSiteKey: nil,
            isForm: false,
            isSignedIn: false,
            showSignIn: { signInCount += 1 }
        )
        try await ViewHosting.host(signedOutSurface) {
            for _ in 0 ..< 20 {
                if (try? signedOutSurface.inspect().find(button: "Sign In")) != nil {
                    break
                }
                try await Task.sleep(nanoseconds: 25_000_000)
            }
            try signedOutSurface.inspect().find(button: "Sign In").tap()
            XCTAssertEqual(signInCount, 1)
            XCTAssertThrowsError(try signedOutSurface.inspect().find(button: "Support"))
            XCTAssertThrowsError(try signedOutSurface.inspect().find(button: "Clear"))
        }
    }
}
