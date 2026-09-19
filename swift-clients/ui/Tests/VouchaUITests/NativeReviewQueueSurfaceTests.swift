import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeReviewQueueSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testRendersLoadingEmptyErrorAndAuthorizationStates() throws {
        let signedOut = NativeReviewQueueViewModel(client: nil, isSignedIn: false, isAdministrator: false)
        XCTAssertNoThrow(try NativeReviewQueueSurface(viewModel: signedOut).inspect().find(text: "Sign in required"))

        let member = NativeReviewQueueViewModel(client: nil, isSignedIn: true, isAdministrator: false)
        XCTAssertNoThrow(
            try NativeReviewQueueSurface(viewModel: member).inspect().find(text: "Administrator access required")
        )

        let loading = NativeReviewQueueViewModel(client: nil, isSignedIn: true, isAdministrator: true)
        loading.state = .loading
        XCTAssertNoThrow(try NativeReviewQueueSurface(viewModel: loading).inspect().find(text: "Loading review queue"))

        loading.state = .empty
        XCTAssertNoThrow(try NativeReviewQueueSurface(viewModel: loading).inspect().find(text: "Review queue is empty"))

        loading.state = .error
        loading.errorMessage = .verbatim("Queue failed")
        let error = try NativeReviewQueueSurface(viewModel: loading).inspect()
        XCTAssertNoThrow(try error.find(text: "Queue failed"))
        XCTAssertNoThrow(try error.find(button: "Retry"))
    }

    func testRendersReviewFieldsFallbacksAndActionButtons() async throws {
        let viewModel = NativeReviewQueueViewModel(client: nil, isSignedIn: true, isAdministrator: true)
        viewModel.items = try [NativeReviewQueueItem(post: decodedPost(), clearanceStatus: .rejected)]
        viewModel.state = .loaded
        viewModel.hasNextPage = true
        viewModel.endCursor = "post-1"
        let createdAt = NativeReviewQueueRow.createdTimestamp(
            viewModel.items[0],
            locale: uiEnglishTestLocale,
            timeZone: uiTestTimeZone
        )
        XCTAssertEqual(createdAt, uiEnglishDate(viewModel.items[0].post.createdAt))

        var sut = NativeReviewQueueSurface(viewModel: viewModel)
        let inspected = sut.on(\.inspectionDidAppear) { inspection in
            let renderedTexts = try inspection.findAll(ViewType.Text.self).map {
                try normalizedUiText($0.string())
            }
            for text in [
                "Untitled post", "No preview available.", "Author: Anonymous", "Post type: comment",
                "Root thread: discussion · root-slug · root-1", "Moderation summary", "Requires review",
                "0 flagged categories", "1 signal",
                "Created: \(createdAt)"
            ] {
                XCTAssertTrue(renderedTexts.contains(normalizedUiText(text)), "Missing \(text)")
            }
            XCTAssertFalse(renderedTexts.contains { $0.contains("Spam") })
            XCTAssertFalse(renderedTexts.contains { $0.contains("OpenAI") })
            XCTAssertFalse(renderedTexts.contains { $0.contains("spam_signal") })
            XCTAssertNoThrow(try inspection.find(button: "Approve"))
            XCTAssertNoThrow(try inspection.find(button: "Reject"))
            XCTAssertFalse(try inspection.find(button: "Mark for re-review").isDisabled())
            XCTAssertNoThrow(try inspection.find(button: "Refresh"))
            XCTAssertNoThrow(try inspection.find(button: "Load more"))
        }
        ViewHosting.host(view: sut
            .environment(\.locale, uiEnglishTestLocale)
            .environment(\.timeZone, uiTestTimeZone))
        defer { ViewHosting.expel() }
        await fulfillment(of: [inspected], timeout: 1)
    }

    func testNonterminalDrainedPageShowsLoadMoreWithoutEmptyCopy() throws {
        let viewModel = NativeReviewQueueViewModel(client: nil, isSignedIn: true, isAdministrator: true)
        viewModel.state = .loaded
        viewModel.hasNextPage = true
        viewModel.endCursor = "opaque-next"

        let inspection = try NativeReviewQueueSurface(viewModel: viewModel).inspect()

        XCTAssertThrowsError(try inspection.find(text: "Review queue is empty"))
        XCTAssertThrowsError(try inspection.find(text: "There are no posts to review."))
        XCTAssertNoThrow(try inspection.find(button: "Load more"))
    }

    func testDisablesActionsForListAndRowWork() async throws {
        let viewModel = NativeReviewQueueViewModel(client: nil, isSignedIn: true, isAdministrator: true)
        let item = try NativeReviewQueueItem(post: decodedPost(status: "in_review"), clearanceStatus: .inReview)
        viewModel.items = [item]
        viewModel.state = .loaded
        viewModel.hasNextPage = true
        viewModel.endCursor = item.id
        viewModel.isListLoading = true

        var sut = NativeReviewQueueSurface(viewModel: viewModel)
        let listLoadingInspection = sut.on(\.inspectionDidAppear) { inspection in
            XCTAssertNoThrow(try inspection.find(text: "Requires review"))
            XCTAssertTrue(try inspection.find(button: "Approve").isDisabled())
            XCTAssertTrue(try inspection.find(button: "Reject").isDisabled())
            XCTAssertTrue(try inspection.find(button: "Mark for re-review").isDisabled())
            XCTAssertTrue(try inspection.find(button: "Refresh").isDisabled())
            XCTAssertTrue(try inspection.find(button: "Load more").isDisabled())
        }
        ViewHosting.host(view: sut.environment(\.locale, uiEnglishTestLocale))
        await fulfillment(of: [listLoadingInspection], timeout: 1)
        ViewHosting.expel()

        viewModel.isListLoading = false
        viewModel.inFlightPostIds.insert(item.id)
        sut = NativeReviewQueueSurface(viewModel: viewModel)
        let rowLoadingInspection = sut.on(\.inspectionDidAppear) { inspection in
            XCTAssertTrue(try inspection.find(button: "Approve").isDisabled())
            XCTAssertTrue(try inspection.find(button: "Refresh").isDisabled())
            XCTAssertTrue(try inspection.find(button: "Load more").isDisabled())
        }
        ViewHosting.host(view: sut.environment(\.locale, uiEnglishTestLocale))
        defer { ViewHosting.expel() }
        await fulfillment(of: [rowLoadingInspection], timeout: 1)
    }

    func testReviewQueueRouteUsesDedicatedSurfaceWithoutGenericLoad() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/posts/review-queue"))
        let client = try makeClient()
        let surface = NativeRouteDestinationSurface(
            entry: route.entry,
            client: client,
            routeMatch: route.match,
            routeQuery: nil,
            isSignedIn: true,
            isAdministrator: true,
            showSignIn: {}
        )

        XCTAssertFalse(surface.shouldLoadRouteSurfaceContent)
        XCTAssertNoThrow(try surface.destinationContent.inspect().find(NativeReviewQueueSurface.self))
        XCTAssertThrowsError(try surface.destinationContent.inspect().find(NativeListSurface.self))

        let generic = NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: client,
            routeMatch: route.match,
            isAdministrator: true
        )
        await generic.load()
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testAuthAndRoleReplacementChangesLoadTaskIdentity() throws {
        let client = try makeClient()
        let signedOut = NativeReviewQueueViewModel(client: client, isSignedIn: false, isAdministrator: false)
        let member = NativeReviewQueueViewModel(client: client, isSignedIn: true, isAdministrator: false)
        let administrator = NativeReviewQueueViewModel(client: client, isSignedIn: true, isAdministrator: true)

        let signedOutSurface = NativeReviewQueueSurface(viewModel: signedOut)
        let memberSurface = NativeReviewQueueSurface(viewModel: member)
        let administratorSurface = NativeReviewQueueSurface(viewModel: administrator)

        XCTAssertEqual(signedOutSurface.loadTaskIdentity, signedOut.loadTaskId)
        XCTAssertNotEqual(signedOutSurface.loadTaskIdentity, memberSurface.loadTaskIdentity)
        XCTAssertNotEqual(memberSurface.loadTaskIdentity, administratorSurface.loadTaskIdentity)

        signedOut.cancelListOperations()
        member.cancelListOperations()
        XCTAssertNoThrow(try signedOutSurface.inspect().find(text: "Sign in required"))
        XCTAssertNoThrow(try memberSurface.inspect().find(text: "Administrator access required"))
    }

    private func decodedPost(status: String = "rejected") throws -> AdminReviewQueuePost {
        let data = Data(
            #"{"id":"post-1","title":" ","declared_language":null,"lingua_rs_detected_language":null,"slug":null,"markdown_preview":" ","post_type":"comment","created_by_id":null,"created_at":"2026-06-01T11:30:00.000Z","root_id":"root-1","root_post_type":"discussion","root_slug":"root-slug","clearance_status":"\#(status)","clearance_updated_at":null,"moderation_summary":{"disposition":"review","evidence_summary":{"flagged_category_count":0,"signal_count":1},"reason_codes":["spam_signal"]},"media_reveal":{"requires_reveal":false,"images":[]}}"#
                .utf8
        )
        return try JSONDecoder.vouchaFixtureDecoder.decode(AdminReviewQueuePost.self, from: data)
    }

}
