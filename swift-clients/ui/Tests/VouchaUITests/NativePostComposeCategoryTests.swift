import Foundation
import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativePostComposeCategoryTests: XCTestCase {
    func testCategoryDraftsPreserveAuthoredTokenInAtomicCreateBody() throws {
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.categoryDrafts = [
            .init(type: .topic, value: " topic-1 "),
            .init(type: .hashtag, value: " #Me.Too__2026 "),
            .init(type: .hashtag, value: "   ")
        ]

        let endpoint = viewModel.makeCreateEndpoint(
            turnstileToken: nil,
            idempotencyKey: "00000000-0000-4000-8000-000000000041"
        )
        let body = try encodedJSONObject(from: XCTUnwrap(endpoint.body))
        let categories = try XCTUnwrap(body["categories"] as? [[String: Any]])

        XCTAssertEqual(categories.count, 2)
        XCTAssertEqual(categories[0]["type"] as? String, "topic")
        XCTAssertEqual(categories[0]["topic_id"] as? String, "topic-1")
        XCTAssertEqual(categories[1]["type"] as? String, "hashtag")
        XCTAssertEqual(categories[1]["hashtag"] as? String, "#Me.Too__2026")
    }

    func testCanonicalIntentChangesWhenAnAtomicCategoryChanges() {
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.bodyText = "Discussion body"
        let baseline = viewModel.contributionCanonicalIntent
        viewModel.categoryDrafts = [.init(type: .hashtag, value: "#Swift_UI")]

        XCTAssertNotEqual(baseline, viewModel.contributionCanonicalIntent)
    }

    func testCategoryDraftActionsUpdateAndRemoveByStableId() {
        let viewModel = NativePostComposeViewModel(client: nil)

        viewModel.addCategory()
        let id = viewModel.categoryDrafts[0].id
        viewModel.updateCategoryType(id: id, type: .hashtag)
        viewModel.updateCategoryValue(id: id, value: "#Swift_UI")

        XCTAssertEqual(viewModel.categoryDrafts.first?.type, .hashtag)
        XCTAssertEqual(viewModel.categoryDrafts.first?.value, "#Swift_UI")

        viewModel.removeCategory(id: id)
        XCTAssertTrue(viewModel.categoryDrafts.isEmpty)
    }

    func testInvalidNonblankHashtagDraftBlocksDiscussionSubmissionButValidAndBlankDraftsCanSubmit() {
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.bodyText = "Discussion body"
        viewModel.categoryDrafts = [.init(type: .hashtag, value: "not valid")]

        XCTAssertFalse(viewModel.canSubmit)

        viewModel.categoryDrafts = [
            .init(type: .hashtag, value: "   "),
            .init(type: .hashtag, value: "#Swift_UI")
        ]

        XCTAssertTrue(viewModel.canSubmit)
    }

    func testDiscussionCategorySurfaceRendersControlsAndRemovesDraft() throws {
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.categoryDrafts = [.init(type: .hashtag, value: "#Swift_UI")]
        let surface = NativePostComposeSurface(client: nil)

        let sut = surface.discussionCategorySection(viewModel: viewModel)
        XCTAssertNoThrow(try sut.inspect().find(text: "Categories"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Search hashtags"))

        try sut.inspect().find(button: "Remove").tap()
        XCTAssertTrue(viewModel.categoryDrafts.isEmpty)
    }

    func testResetFormClearsCategoryDrafts() {
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.categoryDrafts = [.init(type: .topic, value: "topic-1")]

        viewModel.resetFormAfterPublish()

        XCTAssertTrue(viewModel.categoryDrafts.isEmpty)
    }

    private func encodedJSONObject(from body: any Encodable) throws -> [String: Any] {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let data = try encoder.encode(body)
        return try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
    }
}
