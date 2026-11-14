import Foundation
import SwiftUI
import ViewInspector
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import XCTest

@MainActor
final class ErrorStateViewTests: XCTestCase {
    func testIdentifiedLocalizedRetryButtonInvokesRetry() async throws {
        let retried = expectation(description: "Retry callback completes")
        let view = ErrorStateView(error: VouchaError.api(statusCode: 500, preconditionCode: nil)) {
            retried.fulfill()
        }
        .environment(\.locale, Locale(identifier: "en_US"))
        let button = try view.inspect().find(button: UiMessages.string(.nativeCommonRetry, locale: .english))

        XCTAssertEqual(try button.accessibilityIdentifier(), "error-state-retry")
        try button.tap()

        await fulfillment(of: [retried], timeout: 1)
    }
}
