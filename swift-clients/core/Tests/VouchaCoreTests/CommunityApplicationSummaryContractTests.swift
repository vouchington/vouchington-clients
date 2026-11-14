import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class CommunityApplicationSummaryContractTests: XCTestCase {
    func testSummaryFixtureCarriesPreviewCountStatusAndApplicantWithoutAnswers() throws {
        let data = ApiFixtureLoader.data("web.communities.applications.default")
        let response = try makeVouchaDecoder().decode(CommunityApplicationsResponse.self, from: data)
        let row = try XCTUnwrap(response.communityApplicationSummaries["application-1"])
        XCTAssertEqual(row.status, "pending")
        XCTAssertEqual(row.answerCount, 0)
        XCTAssertEqual(row.messagePreview, "I would like to join.")
        XCTAssertEqual(response.users[row.userId]?.username, "testuser")
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(row)) as? [String: Any])
        XCTAssertNil(object["answers"])
        XCTAssertNil(object["message"])
    }

    func testDetailFixtureDecodesFullMessageAndAnswerValues() throws {
        let data = ApiFixtureLoader.data("web.communities.application.default")
        let decoder = makeVouchaDecoder()
        let detail = try decoder.decode(CommunityApplicationResponse.self, from: data).communityApplication
        XCTAssertEqual(detail.id, "application-1")
        XCTAssertEqual(detail.message, "I would like to join.")
        XCTAssertTrue(detail.answers.isEmpty)

        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var application = try XCTUnwrap(object["community_application"] as? [String: Any])
        application["message"] = String(repeating: "Full application text. ", count: 20)
        application["answers"] = ["why": "I participate regularly", "agreement": true]
        object["community_application"] = application
        let full = try decoder.decode(
            CommunityApplicationResponse.self,
            from: JSONSerialization.data(withJSONObject: object)
        ).communityApplication
        XCTAssertGreaterThan(try XCTUnwrap(full.message).count, 200)
        guard case let .string(answer) = full.answers["why"],
              case let .bool(agreement) = full.answers["agreement"] else {
            return XCTFail("Detail must retain answer values")
        }
        XCTAssertEqual(answer, "I participate regularly")
        XCTAssertTrue(agreement)
        assertEndpoint(
            Endpoint.communityApplication(idOrSlug: "test-community", applicationId: "application-1"),
            path: "/api/v1/communities/test-community/applications/application-1"
        )
    }

}
