import Foundation
import Observation
import VouchaAuth
@testable import VouchaFeatures
import XCTest

@MainActor
final class MutationChallengeBrokerTests: XCTestCase {
    func testOnePromptCannotShareProofWithConcurrentMutationOrStaleWidget() async throws {
        let broker = MutationChallengeBroker()
        let presented = observePresentation(broker)
        let completed = expectation(description: "Mutation receives proof")
        var token: String?
        let first = Task {
            token = await broker.request()
            completed.fulfill()
        }
        defer { first.cancel()
            broker.cancel()
        }
        await fulfillment(of: [presented], timeout: 2)
        let id = try XCTUnwrap(broker.pendingID)
        let concurrent = await broker.request()
        XCTAssertNil(concurrent)
        broker.finish(id: UUID(), token: "stale")
        XCTAssertEqual(broker.pendingID, id)
        broker.finish(id: id, token: "proof")
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(token, "proof")
        XCTAssertNil(broker.pendingID)
    }

    func testCancellationDismissesPromptAndIgnoresLateToken() async throws {
        let broker = MutationChallengeBroker()
        let presented = observePresentation(broker)
        let completed = expectation(description: "Cancelled mutation resumes")
        var token: String? = "unresolved"
        let request = Task {
            token = await broker.request()
            completed.fulfill()
        }
        defer { request.cancel()
            broker.cancel()
        }
        await fulfillment(of: [presented], timeout: 2)
        let id = try XCTUnwrap(broker.pendingID)
        request.cancel()
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertNil(token)
        XCTAssertNil(broker.pendingID)
        broker.finish(id: id, token: "late")
        XCTAssertNil(broker.pendingID)
    }

    func testAccountOrPresentationDismissalResumesWithoutProof() async throws {
        let broker = MutationChallengeBroker()
        let presented = observePresentation(broker)
        let completed = expectation(description: "Dismissed mutation resumes")
        var token: String? = "unresolved"
        let request = Task {
            token = await broker.request()
            completed.fulfill()
        }
        defer { request.cancel()
            broker.cancel()
        }
        await fulfillment(of: [presented], timeout: 2)
        _ = try XCTUnwrap(broker.pendingID)
        broker.cancel()
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertNil(token)
    }

    private func observePresentation(_ broker: MutationChallengeBroker) -> XCTestExpectation {
        let presented = expectation(description: "Native challenge presented")
        withObservationTracking { _ = broker.pendingID } onChange: { presented.fulfill() }
        return presented
    }
}
