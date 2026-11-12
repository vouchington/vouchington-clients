import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class MutationChallengeBrokerTests: XCTestCase {
    func testOnePromptCannotShareProofWithConcurrentMutationOrStaleWidget() async throws {
        let broker = MutationChallengeBroker()
        let first = Task { await broker.request() }
        while broker.pendingID == nil {
            await Task.yield()
        }
        let id = try XCTUnwrap(broker.pendingID)
        let concurrent = await broker.request()
        XCTAssertNil(concurrent)
        broker.finish(id: UUID(), token: "stale")
        XCTAssertEqual(broker.pendingID, id)
        broker.finish(id: id, token: "proof")
        let token = await first.value
        XCTAssertEqual(token, "proof")
        XCTAssertNil(broker.pendingID)
    }

    func testCancellationDismissesPromptAndIgnoresLateToken() async throws {
        let broker = MutationChallengeBroker()
        let request = Task { await broker.request() }
        while broker.pendingID == nil {
            await Task.yield()
        }
        let id = try XCTUnwrap(broker.pendingID)
        request.cancel()
        let token = await request.value
        XCTAssertNil(token)
        XCTAssertNil(broker.pendingID)
        broker.finish(id: id, token: "late")
        XCTAssertNil(broker.pendingID)
    }

    func testAccountOrPresentationDismissalResumesWithoutProof() async {
        let broker = MutationChallengeBroker()
        let request = Task { await broker.request() }
        while broker.pendingID == nil {
            await Task.yield()
        }
        broker.cancel()
        let token = await request.value
        XCTAssertNil(token)
    }
}
