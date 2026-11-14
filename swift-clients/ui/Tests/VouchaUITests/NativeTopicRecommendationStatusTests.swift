import Foundation
import ViewInspector
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativeTopicRecommendationStatusTests: XCTestCase {
    private func surface() -> NativeTopicRecommendationSurface {
        NativeTopicRecommendationSurface(client: nil, recommendationId: nil, turnstileSiteKey: "test-key")
    }

    func testRequiredStatusExplainsVerificationBeforeSaving() throws {
        let model = NativeTopicRecommendationViewModel(client: nil)
        model.state = .required
        let expected = UiMessages.string(.nativeSwiftPostComposeVerificationTokenRequired, locale: .current)

        XCTAssertEqual(try surface().status(viewModel: model).inspect().find(text: expected).string(), expected)
    }

    func testErrorStatusDisplaysFailureInsteadOfSavedReceipt() throws {
        let model = NativeTopicRecommendationViewModel(client: nil)
        let error = VouchaError.unexpected("Cannot save recommendation")
        model.state = .error(error)
        model.savedPostId = "old-receipt"

        let inspected = try surface().status(viewModel: model).inspect()
        XCTAssertEqual(try inspected.find(text: error.localizedDescription).string(), error.localizedDescription)
        XCTAssertEqual(inspected.findAll(ViewType.Text.self).count, 1)
    }

    func testSavedStatusIncludesReceiptAndIdleStatusIsEmpty() throws {
        let model = NativeTopicRecommendationViewModel(client: nil)
        XCTAssertThrowsError(try surface().status(viewModel: model).inspect().find(ViewType.Text.self))
        model.savedPostId = "recommendation-42"
        model.state = .loaded
        let expected = UiMessages.string(
            .nativeSwiftTopicRecommendationSavedValue,
            parameters: ["value": "recommendation-42"],
            locale: .current
        )

        XCTAssertEqual(try surface().status(viewModel: model).inspect().find(text: expected).string(), expected)
    }
}
