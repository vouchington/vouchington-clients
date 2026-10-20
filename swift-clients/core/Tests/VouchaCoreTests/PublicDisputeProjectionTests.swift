import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import VouchaModels
import XCTest

final class PublicDisputeProjectionTests: XCTestCase {
    @MainActor
    func testPublicDetailLoadsThroughRealAPIClientAndTypedEndpoint() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        let host = "public-dispute-\(UUID().uuidString.lowercased()).example.test"
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://\(host)"))),
            cookieStorage: jar,
            protocolClasses: [PublicDisputeReadURLProtocol.self],
            bootstrapSession: false,
            sessionConfiguration: configuration
        )
        let id = "00000000-0000-7000-8000-000000000201"

        let response: ModerationDisputeReadResponse = try await client.send(.dispute(id: id))

        XCTAssertEqual(response.dispute.id, id)
        guard case let .member(dispute) = response.dispute else {
            return XCTFail("The public GET response must retain the member projection")
        }
        XCTAssertEqual(dispute.postContent?.text, "A disputed review")
        XCTAssertEqual(dispute.reason, .other)
    }

    func testMemberListDecodesAndReencodesWithoutStaffFields() throws {
        let data = ApiFixtureLoader.data("native.moderation.disputes.member.default")
        let response = try makeVouchaDecoder().decode(ModerationDisputeListResponse.self, from: data)
        let dispute = try XCTUnwrap(response.disputes.first)
        XCTAssertEqual(dispute.id, "00000000-0000-7000-8000-000000000201")
        XCTAssertEqual(dispute.postId, "post-1")
        XCTAssertEqual(dispute.status, .pending)

        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.dateEncodingStrategy = .iso8601
        let encoded = try encoder.encode(response)
        let originalObject = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let encodedObject = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        let originalRows = try XCTUnwrap(originalObject["disputes"] as? [[String: Any]])
        let encodedRows = try XCTUnwrap(encodedObject["disputes"] as? [[String: Any]])
        XCTAssertEqual(try Set(XCTUnwrap(encodedRows.first).keys), try Set(XCTUnwrap(originalRows.first).keys))
        XCTAssertTrue(try Self.staffFields.isDisjoint(with: Set(XCTUnwrap(encodedRows.first).keys)))
    }

    func testStaffMissingRequiredPrivateFieldsCannotDowngradeToPublic() throws {
        let data = ApiFixtureLoader.data("native.moderation.disputes.default")
        let requiredFields = [
            "claim_text", "disputant_user_id", "ai_drafted_at", "ai_internal_response",
            "ai_public_response", "approved_by_id", "drafted_at", "edited_at", "edited_by_id",
            "internal_notes", "latest_lifecycle_change_id", "model", "resolved_by_id"
        ]
        for key in requiredFields {
            var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
            var rows = try XCTUnwrap(object["disputes"] as? [[String: Any]])
            rows[0].removeValue(forKey: key)
            object["disputes"] = rows
            XCTAssertThrowsError(
                try makeVouchaDecoder().decode(
                    ModerationDisputeListResponse.self,
                    from: JSONSerialization.data(withJSONObject: object)
                ),
                key
            )
        }
    }

    func testMemberAndStaffDetailKeepSameIdentityAndDistinctProjections() throws {
        let memberData = ApiFixtureLoader.data("native.moderation.disputes.detail.member.default")
        let staffData = ApiFixtureLoader.data("native.moderation.disputes.detail.default")
        let member = try makeVouchaDecoder().decode(ModerationDisputeReadResponse.self, from: memberData)
        let staff = try makeVouchaDecoder().decode(ModerationDisputeReadResponse.self, from: staffData)
        guard case let .member(publicDispute) = member.dispute,
              case let .staff(staffDispute) = staff.dispute
        else { return XCTFail("Authorized response projections were not retained") }
        XCTAssertEqual(member.dispute.id, staff.dispute.id)
        XCTAssertEqual(member.dispute.postId, staff.dispute.postId)
        XCTAssertEqual(member.dispute.status, staff.dispute.status)
        XCTAssertEqual(publicDispute.postContent?.text, staffDispute.postContent?.text)
        XCTAssertEqual(staffDispute.claimText, "Native review dispute")

        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.dateEncodingStrategy = .iso8601
        let encoded = try encoder.encode(member)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        let dispute = try XCTUnwrap(object["dispute"] as? [String: Any])
        let original = try XCTUnwrap(JSONSerialization.jsonObject(with: memberData) as? [String: Any])
        let originalDispute = try XCTUnwrap(original["dispute"] as? [String: Any])
        XCTAssertEqual(Set(dispute.keys), Set(originalDispute.keys))
        XCTAssertTrue(Self.staffFields.isDisjoint(with: Set(dispute.keys)))
        assertEndpoint(.dispute(id: publicDispute.id), path: "/api/v1/disputes/\(publicDispute.id)")
    }

    func testAnyProtectedFieldPreventsPublicFallback() throws {
        let data = ApiFixtureLoader.data("native.moderation.disputes.detail.member.default")
        for key in Self.staffFields {
            var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
            var dispute = try XCTUnwrap(object["dispute"] as? [String: Any])
            dispute[key] = NSNull()
            object["dispute"] = dispute
            XCTAssertThrowsError(
                try makeVouchaDecoder().decode(
                    ModerationDisputeReadResponse.self,
                    from: JSONSerialization.data(withJSONObject: object)
                ),
                key
            )
        }
    }

    func testOptionalOverdueOmissionAndNullPostContentRemainPublic() throws {
        let data = ApiFixtureLoader.data("native.moderation.disputes.detail.member.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var dispute = try XCTUnwrap(object["dispute"] as? [String: Any])
        dispute.removeValue(forKey: "is_overdue")
        dispute["post_content"] = NSNull()
        object["dispute"] = dispute
        let response = try makeVouchaDecoder().decode(
            ModerationDisputeReadResponse.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        guard case let .member(member) = response.dispute else {
            return XCTFail("Public content fields must not select the staff projection")
        }
        XCTAssertNil(member.isOverdue)
        XCTAssertNil(member.postContent)
    }

    static let staffFields: Set<String> = [
        "disputant_user_id", "claim_text", "recommended_action", "ai_public_response",
        "ai_internal_response", "model", "ai_drafted_at", "internal_notes", "drafted_at",
        "edited_at", "edited_by_id", "approved_by_id", "resolved_by_id",
        "latest_lifecycle_change_id", "staff_context", "ai_draft_run_id", "ai_draft_requested_run_id"
    ]
}
