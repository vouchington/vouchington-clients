import Foundation
@testable import VouchaAPI
import XCTest

let communityLimitFixtureCoverage: [RegisteredFixture] = [
    "native.communities.membership-cap.join.conflict",
    "native.communities.moderator-cap.promotion.conflict"
].map { id in
    RegisteredFixture(id: id) {
        let data = ApiFixtureLoader.data($0)
        let payload = try JSONDecoder().decode(APIErrorPayload.self, from: data)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: String])
        XCTAssertEqual(payload.code, object["code"])
        XCTAssertEqual(payload.displayMessage, object["message"])
        XCTAssertEqual(JsonKeyPaths.extract(data), Set(["code", "message"]))
    }
}

let communityLimitFixtureEndpoints: [String: Endpoint] = [
    "native.communities.membership-cap.join.conflict": .joinCommunity(idOrSlug: "test-community"),
    "native.communities.moderator-cap.promotion.conflict": .updateCommunityMemberRole(
        idOrSlug: "test-community", userId: "00000000-0000-7000-8000-000000000904", role: .moderator
    )
]
