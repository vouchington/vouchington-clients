import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class CommunityBanContractTests: XCTestCase {
    func testProducerBanResponses() throws {
        let decoder = JSONDecoder.vouchaFixtureDecoder
        let active = try decoder.decode(
            CommunityBansResponse.self,
            from: ApiFixtureLoader.data("native.communities.bans.active.default")
        )
        XCTAssertEqual(active.results.count, 1)
        XCTAssertNil(active.communityBans.values.first?.reason)
        let detail = try decoder.decode(
            CommunityBanResponse.self,
            from: ApiFixtureLoader.data("native.communities.bans.detail.default")
        )
        XCTAssertEqual(detail.communityBan.reason, "Repeated spam")
        let one = try decoder.decode(
            CommunityBanLiftResponse.self,
            from: ApiFixtureLoader.data("native.communities.bans.lift-one.default")
        )
        let all = try decoder.decode(
            CommunityBanLiftResponse.self,
            from: ApiFixtureLoader.data("native.communities.bans.lift-all.default")
        )
        XCTAssertEqual(one.liftedBanIds.count, 1)
        XCTAssertEqual(all.liftedBanIds.count, 3)
        XCTAssertEqual(one.liftedBanIds.first, active.results.first?.id)
    }

    func testProducerStableErrorsRetainNumericCap() throws {
        let forbidden = try JSONDecoder().decode(
            APIErrorPayload.self,
            from: ApiFixtureLoader.data("native.communities.bans.lift-all.forbidden")
        )
        let overflow = try JSONDecoder().decode(
            APIErrorPayload.self,
            from: ApiFixtureLoader.data("native.communities.bans.lift-all.overflow")
        )
        XCTAssertEqual(forbidden.code, "COMMUNITY_BAN_LIFT_ALL_FORBIDDEN")
        XCTAssertEqual(overflow.code, "COMMUNITY_BAN_LIFT_ALL_LIMIT_REACHED")
        XCTAssertEqual(overflow.cap, 1)
    }
}
