import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class ReportJudgementAdmissionStoreTests: XCTestCase {
    func testExactTokenAndOwnerGuardAdmissionUntilExplicitTerminalRemoval() throws {
        let store = ReportJudgementAdmissionStore()
        let owner = AppealAIRerunOwner(userId: "alice", baseURL: URL(string: "https://api.test"))
        let other = AppealAIRerunOwner(userId: "bob", baseURL: URL(string: "https://api.test"))
        XCTAssertNil(store.admit("report", owner: nil))
        XCTAssertNil(store.admission("report", owner: nil))
        store.record("run", reportId: "report", owner: nil, token: UUID())
        store.remove("report", owner: nil, token: UUID())
        let admitted = try XCTUnwrap(store.admit("report", owner: owner))
        XCTAssertNil(store.admit("report", owner: owner))
        XCTAssertNil(store.admission("report", owner: other))
        store.record("", reportId: "report", owner: owner, token: admitted.token)
        store.record("run", reportId: "report", owner: owner, token: UUID())
        XCTAssertNil(store.admission("report", owner: owner)?.runId)
        store.record("run", reportId: "report", owner: owner, token: admitted.token)
        XCTAssertEqual(store.admission("report", owner: owner)?.runId, "run")
        store.remove("report", owner: owner, token: UUID())
        XCTAssertNotNil(store.admission("report", owner: owner))
        store.remove("report", owner: owner, token: admitted.token)
        XCTAssertNil(store.admission("report", owner: owner))
    }
}
