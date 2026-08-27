@testable import VouchaFeatures
import VouchaModels
import XCTest

final class ElectionVoteCountReconcilerTests: XCTestCase {
    func testReconcilesSignChangesAndClear() {
        XCTAssertEqual(
            ElectionVoteCountReconciler.reconcile(previous: .like, next: .vouch, positive: 4, negative: 1),
            .init(positive: 4, negative: 1)
        )
        XCTAssertEqual(
            ElectionVoteCountReconciler.reconcile(previous: .like, next: .dislike, positive: 4, negative: 1),
            .init(positive: 3, negative: 2)
        )
        XCTAssertEqual(
            ElectionVoteCountReconciler.reconcile(previous: .dislike, next: nil, positive: 4, negative: 1),
            .init(positive: 4, negative: 0)
        )
    }

    func testDoesNotMakeCountsNegativeWhenReconciliationReceivesStaleCounts() {
        XCTAssertEqual(
            ElectionVoteCountReconciler.reconcile(previous: .oppose, next: nil, positive: 0, negative: 0),
            .init(positive: 0, negative: 0)
        )
    }
}
