import Foundation
import VouchaModels
import XCTest

final class CopyrightFormScreeningDecodingTests: XCTestCase {
    func testScreeningKeepsItsAttemptIdAndStateForEveryProtocolState() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        for state in ["pending", "failed", "completed", "future-state"] {
            let json = Data("""
            {"attempt_id":"attempt-1","state":"\(state)","recommendation":null,"rationale":null,"guidance":null}
            """.utf8)
            let screening = try decoder.decode(CopyrightStaffFormScreening.self, from: json)
            XCTAssertEqual(screening.attemptId, "attempt-1")
            XCTAssertEqual(screening.state, state)
        }
    }
}
