import Foundation
import Observation
import SwiftUI
import ViewInspector
import VouchaAuth
@testable import VouchaFeatures
import XCTest

@MainActor
final class MutationChallengePresentationTests: XCTestCase {
    func testPresentedProviderUsesMutationActionConfiguredOriginAndReturnsProof() async throws {
        let broker = MutationChallengeBroker()
        let presented = observePresentation(broker)
        let completed = expectation(description: "Mutation receives provider proof")
        var proof: String?
        let request = Task {
            proof = await broker.request()
            completed.fulfill()
        }
        defer { request.cancel()
            broker.cancel()
        }
        await fulfillment(of: [presented], timeout: 2)
        _ = try XCTUnwrap(broker.pendingID)
        let origin = try XCTUnwrap(URL(string: "https://native.example:8443/api"))
        let surface = EmptyView().mutationChallengeSheet(
            broker: broker, siteKey: "configured-mutation-key", baseURL: origin
        )
        let sheet = try surface.inspect().emptyView().sheet()
        let provider = try sheet.find(NativeTurnstileChallengeView.self).actualView()
        XCTAssertEqual(provider.action.rawValue, "bot-challenge.mutation")
        XCTAssertEqual(provider.siteKey, "configured-mutation-key")
        XCTAssertEqual(provider.baseURL, origin)
        let widget = try provider.inspect().find(NativeTurnstileWebView.self).actualView()
        XCTAssertEqual(widget.challenge.action, "bot-challenge-mutation")
        XCTAssertEqual(widget.challenge.origin.absoluteString, "https://native.example:8443")
        XCTAssertEqual(widget.siteKey, "configured-mutation-key")

        provider.onToken("single-use-provider-proof")

        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(proof, "single-use-provider-proof")
        XCTAssertNil(broker.pendingID)
        XCTAssertThrowsError(try surface.inspect().emptyView().sheet())
        provider.onToken("late-provider-proof")
        XCTAssertNil(broker.pendingID)
    }

    func testDismissingPresentedSheetResumesMutationWithoutProof() async throws {
        let broker = MutationChallengeBroker()
        let presented = observePresentation(broker)
        let completed = expectation(description: "Dismissed mutation resumes")
        var proof: String? = "unresolved"
        let request = Task {
            proof = await broker.request()
            completed.fulfill()
        }
        defer { request.cancel()
            broker.cancel()
        }
        await fulfillment(of: [presented], timeout: 2)
        _ = try XCTUnwrap(broker.pendingID)
        let surface = try EmptyView().mutationChallengeSheet(
            broker: broker, siteKey: "configured-key",
            baseURL: XCTUnwrap(URL(string: "https://native.example"))
        )
        let sheet = try surface.inspect().emptyView().sheet()
        XCTAssertNoThrow(try sheet.find(NativeTurnstileChallengeView.self))

        try sheet.dismiss()

        await fulfillment(of: [completed], timeout: 2)
        XCTAssertNil(proof)
        XCTAssertNil(broker.pendingID)
        XCTAssertThrowsError(try surface.inspect().emptyView().sheet())
    }

    private func observePresentation(_ broker: MutationChallengeBroker) -> XCTestExpectation {
        let presented = expectation(description: "Native challenge presented")
        withObservationTracking { _ = broker.pendingID } onChange: { presented.fulfill() }
        return presented
    }
}
