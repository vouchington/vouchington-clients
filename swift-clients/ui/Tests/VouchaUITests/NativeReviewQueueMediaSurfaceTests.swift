import Foundation
import SwiftUI
import ViewInspector
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeReviewQueueMediaSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testSensitiveMediaIsConcealedUntilRevealThenRendersWholeGroup() throws {
        let viewModel = try makeViewModel(posts: [decodedPost(id: "sensitive", requiresReveal: true)])
        viewModel.exposureState = try decodedExposure(inCooldown: false)
        viewModel.exposureIsStale = false

        var inspection = try NativeReviewQueueSurface(viewModel: viewModel).inspect()

        XCTAssertNoThrow(try inspection.find(text: "Sensitive media is hidden."))
        XCTAssertNoThrow(try inspection.find(button: "Reveal"))
        XCTAssertTrue(inspection.findAll(ViewType.View<AsyncImageView>.self).isEmpty)

        viewModel.revealedPostIds.insert("sensitive")
        inspection = try NativeReviewQueueSurface(viewModel: viewModel).inspect()

        let images = inspection.findAll(ViewType.View<AsyncImageView>.self)
        XCTAssertEqual(images.count, 2)
        XCTAssertEqual(try images[0].actualView().resolvedURLString, "https://images.voucha.ai/images/image-1")
        XCTAssertEqual(try images[1].actualView().resolvedURLString, "https://images.voucha.ai/images/image-2")
        XCTAssertNoThrow(try inspection.find(text: "First image"))
        XCTAssertNoThrow(try inspection.find(text: "Second image"))
    }

    func testNonSensitiveMediaRendersWhileSensitiveRevealIsGated() throws {
        let posts = try [
            decodedPost(id: "sensitive", requiresReveal: true),
            decodedPost(id: "safe", requiresReveal: false)
        ]
        let viewModel = try makeViewModel(posts: posts)
        viewModel.exposureIsStale = true

        let inspection = try NativeReviewQueueSurface(viewModel: viewModel).inspect()

        XCTAssertEqual(inspection.findAll(ViewType.View<AsyncImageView>.self).count, 2)
        XCTAssertTrue(try inspection.find(button: "Reveal").isDisabled())
        XCTAssertNoThrow(try inspection.find(text: "Exposure status needs an update."))
    }

    func testCooldownAndStaleStatesOfferBreakAndRefetchWithoutDisablingActions() throws {
        let post = try decodedPost(id: "sensitive", requiresReveal: true)
        let viewModel = try makeViewModel(posts: [post])
        viewModel.exposureState = try decodedExposure(inCooldown: true)
        viewModel.exposureIsStale = false

        var inspection = try NativeReviewQueueSurface(viewModel: viewModel).inspect()

        XCTAssertNoThrow(try inspection.find(text: "Take a short break before revealing more sensitive media."))
        XCTAssertNoThrow(try inspection.find(button: "Check again"))
        XCTAssertFalse(try inspection.find(button: "Approve").isDisabled())
        XCTAssertFalse(try inspection.find(button: "Reject").isDisabled())

        viewModel.exposureIsStale = true
        inspection = try NativeReviewQueueSurface(viewModel: viewModel).inspect()
        XCTAssertNoThrow(try inspection.find(text: "Exposure status needs an update."))
        XCTAssertNoThrow(try inspection.find(button: "Check exposure"))
        XCTAssertFalse(try inspection.find(button: "Approve").isDisabled())
    }

    private func makeViewModel(posts: [AdminReviewQueuePost]) throws -> NativeReviewQueueViewModel {
        let viewModel = try NativeReviewQueueViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true
        )
        viewModel.items = posts.map {
            NativeReviewQueueItem(post: $0, clearanceStatus: $0.clearanceStatus)
        }
        viewModel.state = .loaded
        return viewModel
    }

    private func decodedPost(id: String, requiresReveal: Bool) throws -> AdminReviewQueuePost {
        let data = Data(
            """
            {
              "id":"\(id)","title":"Title","slug":"\(id)","markdown_preview":"Preview",
              "post_type":"discussion","created_by_id":"author",
              "created_at":"2026-06-01T11:30:00.000Z","root_id":null,"root_post_type":null,
              "root_slug":null,"clearance_status":"rejected","clearance_updated_at":null,
              "spam_detection_flagged":false,"spam_detection_score":null,"spam_detection_results":{},
              "openai_omni_moderation_flagged":false,"openai_omni_moderation_results":{},
              "media_context":{
                "requires_reveal":\(requiresReveal),
                "images":[
                  {"image_id":"image-1","order_index":0,"caption":"First image"},
                  {"image_id":"image-2","order_index":1,"caption":"Second image"}
                ]
              }
            }
            """.utf8
        )
        return try JSONDecoder.vouchaFixtureDecoder.decode(AdminReviewQueuePost.self, from: data)
    }

    private func decodedExposure(inCooldown: Bool) throws -> ModerationExposureState {
        let data = Data(
            #"{"count":1,"threshold":10,"in_cooldown":\#(inCooldown),"cooldown_ends_at":"2099-07-29T20:00:00.000Z"}"#
                .utf8
        )
        return try JSONDecoder.vouchaFixtureDecoder.decode(ModerationExposureState.self, from: data)
    }
}
