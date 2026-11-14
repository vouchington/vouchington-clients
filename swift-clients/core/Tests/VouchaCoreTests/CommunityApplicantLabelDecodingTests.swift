import Foundation
import VouchaModels
import XCTest

final class CommunityApplicantLabelDecodingTests: XCTestCase {
    func testApplicantFixtureRoundTripsSummaryAndProfiles() throws {
        try assertFixtureCoversDTO("web.communities.applications.default", as: CommunityApplicationsResponse.self)
    }

    func testApplicantSidecarIncludesNonmemberAndIsRequired() throws {
        let data = ApiFixtureLoader.data("web.communities.applications.default")
        let decoder = makeVouchaDecoder()
        let response = try decoder.decode(CommunityApplicationsResponse.self, from: data)
        let application = try XCTUnwrap(response.communityApplicationSummaries["application-1"])
        XCTAssertEqual(response.users[application.userId]?.username, "testuser")
        XCTAssertEqual(application.status, "pending")
        XCTAssertEqual(application.answerCount, 0)
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertNil(object["community_members"])
        object["users"] = [:] as [String: Any]
        let deleted = try decoder.decode(
            CommunityApplicationsResponse.self, from: JSONSerialization.data(withJSONObject: object)
        )
        XCTAssertEqual(deleted.results.count, 1)
        XCTAssertTrue(deleted.users.isEmpty)
        object.removeValue(forKey: "users")
        XCTAssertThrowsError(try decoder.decode(
            CommunityApplicationsResponse.self, from: JSONSerialization.data(withJSONObject: object)
        ))
    }
}
