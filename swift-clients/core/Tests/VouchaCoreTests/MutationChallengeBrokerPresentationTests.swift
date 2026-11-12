import Foundation
import Observation
import VouchaAuth
import XCTest

@MainActor
final class MutationChallengeBrokerPresentationTests: XCTestCase {
    func testOnlyClaimedPresenterCanCompleteOrCancelPrompt() async throws {
        let broker = MutationChallengeBroker()
        let pending = observeRequest(broker)
        let completed = expectation(description: "Owned mutation resumes")
        var proof: String?
        let request = Task {
            proof = await broker.request()
            completed.fulfill()
        }
        defer { request.cancel()
            broker.cancel()
        }
        await fulfillment(of: [pending], timeout: 2)
        let id = try XCTUnwrap(broker.pendingID)
        let owner = UUID()
        let other = UUID()
        XCTAssertFalse(broker.claimPresentation(id: UUID(), owner: other))
        XCTAssertTrue(broker.claimPresentation(id: id, owner: owner))
        XCTAssertTrue(broker.claimPresentation(id: id, owner: owner))
        XCTAssertFalse(broker.claimPresentation(id: id, owner: other))
        broker.finishPresentation(id: id, owner: other, token: nil)
        XCTAssertEqual(broker.pendingID, id)
        broker.finishPresentation(id: id, owner: other, token: "wrong-window")
        XCTAssertEqual(broker.pendingID, id)
        broker.finishPresentation(id: id, owner: owner, token: "owned-proof")
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(proof, "owned-proof")
        XCTAssertNil(broker.pendingID)
    }

    func testCancellationReleasesLeaseAndOldPresenterCannotFinishNextRequest() async throws {
        let broker = MutationChallengeBroker()
        let firstPending = observeRequest(broker)
        let firstCompleted = expectation(description: "Cancelled owned mutation resumes")
        var cancelledProof: String? = "unresolved"
        let first = Task {
            cancelledProof = await broker.request()
            firstCompleted.fulfill()
        }
        defer { first.cancel()
            broker.cancel()
        }
        await fulfillment(of: [firstPending], timeout: 2)
        let firstID = try XCTUnwrap(broker.pendingID)
        let firstOwner = UUID()
        XCTAssertTrue(broker.claimPresentation(id: firstID, owner: firstOwner))
        broker.finishPresentation(id: firstID, owner: firstOwner, token: nil)
        await fulfillment(of: [firstCompleted], timeout: 2)
        XCTAssertNil(cancelledProof)

        let secondPending = observeRequest(broker)
        let secondCompleted = expectation(description: "Next owned mutation resumes")
        var proof: String?
        let second = Task {
            proof = await broker.request()
            secondCompleted.fulfill()
        }
        defer { second.cancel() }
        await fulfillment(of: [secondPending], timeout: 2)
        let secondID = try XCTUnwrap(broker.pendingID)
        let secondOwner = UUID()
        XCTAssertTrue(broker.claimPresentation(id: secondID, owner: secondOwner))
        broker.finishPresentation(id: firstID, owner: firstOwner, token: "late-proof")
        broker.finishPresentation(id: firstID, owner: secondOwner, token: "stale-id")
        XCTAssertEqual(broker.pendingID, secondID)
        broker.finishPresentation(id: secondID, owner: secondOwner, token: "next-proof")
        await fulfillment(of: [secondCompleted], timeout: 2)
        XCTAssertEqual(proof, "next-proof")
    }

    private func observeRequest(_ broker: MutationChallengeBroker) -> XCTestExpectation {
        let pending = expectation(description: "Mutation requests a prompt")
        withObservationTracking { _ = broker.pendingID } onChange: { pending.fulfill() }
        return pending
    }
}
