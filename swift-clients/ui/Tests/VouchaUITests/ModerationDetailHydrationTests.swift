import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class ModerationDetailHydrationTests: XCTestCase {
    func testRollingHydrationOverlapsAtMostFourRequestsAndPreservesSummaryOrder() async throws {
        let firstFour = expectation(description: "First four detail requests overlap")
        let fifth = expectation(description: "Released slot starts fifth request")
        let sixth = expectation(description: "Released slot starts sixth request")
        let gate = HydrationGate { count in
            if count == 4 { firstFour.fulfill() }
            if count == 5 { fifth.fulfill() }
            if count == 6 { sixth.fulfill() }
        }
        let hydration = Task {
            try await hydrateModerationDetails(Array(0 ..< 6)) { try await gate.fetch($0) }
        }
        await fulfillment(of: [firstFour], timeout: 2)
        let initial = await gate.snapshot()
        XCTAssertEqual(initial.started, Set(0 ..< 4))
        XCTAssertEqual(initial.peak, 4)
        await gate.release(3)
        await fulfillment(of: [fifth], timeout: 2)
        let afterFirstRelease = await gate.snapshot()
        XCTAssertEqual(afterFirstRelease.started, Set(0 ..< 5))
        XCTAssertEqual(afterFirstRelease.peak, 4)
        await gate.release(4)
        await fulfillment(of: [sixth], timeout: 2)
        await gate.releaseAll()
        let result = try await hydration.value
        let final = await gate.snapshot()
        XCTAssertEqual(result, Array(0 ..< 6))
        XCTAssertEqual(Array(final.completed.prefix(2)), [3, 4])
        XCTAssertEqual(final.peak, 4)
    }

    func testEmptyPageDoesNotStartDetailRequests() async throws {
        let result: [Int] = try await hydrateModerationDetails([Int]()) { _ in
            XCTFail("An empty page must not fetch a detail")
            return 0
        }
        XCTAssertTrue(result.isEmpty)
    }
}

private actor HydrationGate {
    let onStart: @Sendable (Int) -> Void
    var started: Set<Int> = []
    var completed: [Int] = []
    var active = 0
    var peak = 0
    var released: Set<Int> = []
    var continuations: [Int: CheckedContinuation<Void, Never>] = [:]

    init(onStart: @escaping @Sendable (Int) -> Void) {
        self.onStart = onStart
    }

    func fetch(_ index: Int) async throws -> Int {
        started.insert(index)
        active += 1
        peak = max(peak, active)
        onStart(started.count)
        if !released.contains(index) {
            await withCheckedContinuation { continuations[index] = $0 }
        }
        active -= 1
        completed.append(index)
        return index
    }

    func release(_ index: Int) {
        released.insert(index)
        continuations.removeValue(forKey: index)?.resume()
    }

    func releaseAll() {
        for index in 0 ..< 6 {
            release(index)
        }
    }

    func snapshot() -> (started: Set<Int>, completed: [Int], peak: Int) {
        (started, completed, peak)
    }
}
