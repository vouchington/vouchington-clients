import Foundation
@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class ReferralProgramsByLinkCountTests: XCTestCase {
    func testEndpointUsesTopicsListWithOnlySortAndPaging() {
        let first = Endpoint.referralProgramsByLinkCount()
        XCTAssertEqual(first.method, .GET)
        XCTAssertEqual(first.path, "/api/v1/topics")
        XCTAssertEqual(first.queryItems, [
            URLQueryItem(name: "limit", value: "25"),
            URLQueryItem(name: "sort", value: "link_count")
        ])

        let next = Endpoint.referralProgramsByLinkCount(limit: 100, after: "cursor-1")
        XCTAssertEqual(next.queryItems, [
            URLQueryItem(name: "limit", value: "100"),
            URLQueryItem(name: "sort", value: "link_count"),
            URLQueryItem(name: "after", value: "cursor-1")
        ])
    }

    func testTopicDetailDecodesReferralProgramAttributes() throws {
        let present = try decodeDetail(attributes: [
            "company_topic_id": "company-1",
            "enabled_at": "2026-01-02T03:04:05Z",
            "disabled_at": NSNull()
        ])
        XCTAssertEqual(present.referralProgramAttributes?.companyTopicId, "company-1")
        XCTAssertNotNil(present.referralProgramAttributes?.enabledAt)
        XCTAssertNil(present.referralProgramAttributes?.disabledAt)

        XCTAssertNil(try decodeDetail(attributes: NSNull()).referralProgramAttributes)
        XCTAssertNil(try decodeDetail(attributes: nil).referralProgramAttributes)
    }

    private func decodeDetail(attributes: Any?) throws -> TopicDetailResponse {
        let base = ApiFixtureLoader.data("web.topics.show.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: base) as? [String: Any])
        object["referral_program_attributes"] = attributes
        let data = try JSONSerialization.data(withJSONObject: object)
        return try makeVouchaDecoder().decode(TopicDetailResponse.self, from: data)
    }
}
