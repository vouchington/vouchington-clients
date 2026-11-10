import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import XCTest

final class CommunityLimitErrorTests: XCTestCase {
    func testTransportPreservesBothCodesWithAndWithoutServerMessage() async throws {
        let client = try APIClient(config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))))
        let response = try XCTUnwrap(HTTPURLResponse(
            url: XCTUnwrap(URL(string: "https://voucha.test")), statusCode: 409, httpVersion: nil, headerFields: nil
        ))
        for (id, code, limit) in [
            (
                "native.communities.membership-cap.join.conflict",
                "COMMUNITY_MEMBERSHIP_LIMIT_REACHED",
                CommunityLimit.membership
            ),
            (
                "native.communities.moderator-cap.promotion.conflict",
                "COMMUNITY_MODERATOR_LIMIT_REACHED",
                CommunityLimit.moderator
            )
        ] {
            for withMessage in [true, false] {
                let data = withMessage ? ApiFixtureLoader.data(id) : try JSONSerialization
                    .data(withJSONObject: ["code": code])
                do {
                    try await client.validate(response: response, data: data)
                    XCTFail("Expected \(code) to throw")
                } catch {
                    guard let error = error as? VouchaError else { return XCTFail("Expected API error") }
                    XCTAssertEqual(error.communityLimit, limit)
                    XCTAssertFalse(error.isRecoverableByTurnstileFallback)
                    switch error {
                    case let .api(status, actualCode):
                        XCTAssertFalse(withMessage)
                        XCTAssertEqual(status, 409)
                        XCTAssertEqual(actualCode, code)
                    case let .apiMessage(status, actualCode, message):
                        XCTAssertTrue(withMessage)
                        XCTAssertEqual(status, 409)
                        XCTAssertEqual(actualCode, code)
                        XCTAssertFalse(message.isEmpty)
                    default: XCTFail("Lost API error code")
                    }
                }
            }
        }
    }

    func testUnknownConflictAndOtherStatusesDoNotMapToCaps() {
        XCTAssertNil(VouchaError.api(statusCode: 409, preconditionCode: "UNKNOWN_CONFLICT").communityLimit)
        XCTAssertNil(VouchaError.apiMessage(statusCode: 409, preconditionCode: nil, message: "Conflict").communityLimit)
        XCTAssertNil(VouchaError.api(statusCode: 403, preconditionCode: "COMMUNITY_MEMBERSHIP_LIMIT_REACHED")
            .communityLimit)
        XCTAssertNil(VouchaError.apiMessage(
            statusCode: 500,
            preconditionCode: "COMMUNITY_MODERATOR_LIMIT_REACHED",
            message: "Failure"
        ).communityLimit)
    }
}
