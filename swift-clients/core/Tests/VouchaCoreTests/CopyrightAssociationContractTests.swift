@testable import VouchaAPI
import VouchaModels
import XCTest

final class CopyrightAssociationContractTests: XCTestCase {
    func testAssociationSearchPreservesNullableClaimantAndEmptyContinuation() throws {
        var body = try fixture("web.copyright.email-notice-association-search")
        var notices = try XCTUnwrap(body["copyright_notices"] as? [[String: Any]])
        notices[0]["claimant_display_name"] = NSNull()
        body["copyright_notices"] = notices
        let decoded = try decode(CopyrightAssociationTargetsResponse.self, body)
        XCTAssertNil(try XCTUnwrap(decoded.copyrightNotices.first).claimantDisplayName)
        let encoded = try encodedObject(decoded)
        let encodedNotices = try XCTUnwrap(encoded["copyright_notices"] as? [[String: Any]])
        XCTAssertTrue(encodedNotices[0]["claimant_display_name"] is NSNull)
        notices[0].removeValue(forKey: "claimant_display_name")
        body["copyright_notices"] = notices
        XCTAssertThrowsError(try decode(CopyrightAssociationTargetsResponse.self, body))

        body["copyright_notices"] = []
        body["page_info"] = ["has_next_page": true, "end_cursor": "opaque/+==", "start_cursor": NSNull()]
        let empty = try decode(CopyrightAssociationTargetsResponse.self, body)
        XCTAssertTrue(empty.copyrightNotices.isEmpty)
        XCTAssertTrue(empty.pageInfo.hasNextPage)
        let request = Endpoint.copyrightNoticeAssociationTargets(
            query: "Harbour photograph",
            after: empty.pageInfo.endCursor
        )
        XCTAssertEqual(request.path, "/api/v1/copyright-notices/association-targets")
        XCTAssertEqual(request.queryItems.first { $0.name == "q" }?.value, "Harbour photograph")
        XCTAssertEqual(request.queryItems.first { $0.name == "after" }?.value, "opaque/+==")
        XCTAssertFalse(request.queryItems.contains { $0.name == "limit" })
    }

    func testErasedAccountUsernameIsNullableButCannotBeMissing() throws {
        var body = try fixture("native.copyright.repeat-infringer-accounts")
        var accounts = try XCTUnwrap(body["copyright_repeat_infringer_accounts"] as? [[String: Any]])
        accounts[0]["account_username"] = NSNull()
        body["copyright_repeat_infringer_accounts"] = accounts
        let decoded = try decode(CopyrightRepeatInfringerAccountsResponse.self, body)
        XCTAssertNil(try XCTUnwrap(decoded.copyrightRepeatInfringerAccounts.first).accountUsername)
        let encoded = try encodedObject(decoded)
        let encodedAccounts = try XCTUnwrap(encoded["copyright_repeat_infringer_accounts"] as? [[String: Any]])
        XCTAssertTrue(encodedAccounts[0]["account_username"] is NSNull)
        accounts[0].removeValue(forKey: "account_username")
        body["copyright_repeat_infringer_accounts"] = accounts
        XCTAssertThrowsError(try decode(CopyrightRepeatInfringerAccountsResponse.self, body))
    }

    func testStaffQueuePreservesPlacementAndUsesTargetIdentity() throws {
        let body = try fixture("web.copyright.staff-queue.default")
        let decoded = try decode(CopyrightStaffQueueResponse.self, body)
        let notice = decoded.copyrightNotices[1]
        let match = notice.staydownMatches[0]
        let target = try XCTUnwrap(notice.targets.first { $0.id == match.targetId })
        XCTAssertEqual(notice.actionIntents.first?.targetId, target.id)
        XCTAssertEqual(match.uploadedByUsername, "photo-author")
        XCTAssertEqual(match.imagePlacement?.imageId, match.imageId)
        XCTAssertEqual(match.imagePlacement?.placementId, "00000000-0000-7000-8000-000000000822")
        XCTAssertEqual(match.imagePlacement?.placementRevision, 1)
        XCTAssertNil(notice.staydownMatches[1].uploadedByUsername)
        XCTAssertNil(notice.staydownMatches[1].imagePlacement)
        let encoded = try encodedObject(decoded)
        let encodedNotices = try XCTUnwrap(encoded["copyright_notices"] as? [[String: Any]])
        let encodedMatches = try XCTUnwrap(encodedNotices[1]["staydown_matches"] as? [[String: Any]])
        XCTAssertTrue(encodedMatches[1]["uploaded_by_username"] is NSNull)
        XCTAssertTrue(encodedMatches[1]["image_placement"] is NSNull)

        for (collection, index, key) in [
            ("action_intents", 0, "target_id"), ("staydown_matches", 0, "target_id"),
            ("staydown_matches", 1, "uploaded_by_username"), ("staydown_matches", 1, "image_placement")
        ] {
            var missing = body
            var notices = try XCTUnwrap(missing["copyright_notices"] as? [[String: Any]])
            var rows = try XCTUnwrap(notices[1][collection] as? [[String: Any]])
            rows[index].removeValue(forKey: key)
            notices[1][collection] = rows
            missing["copyright_notices"] = notices
            XCTAssertThrowsError(try decode(CopyrightStaffQueueResponse.self, missing), key)
        }
    }

    func testStaffPlacementTupleCannotBePartial() throws {
        let body = try fixture("web.copyright.staff-queue.default")
        for key in ["image_id", "placement_id", "placement_revision"] {
            var missing = body
            var notices = try XCTUnwrap(missing["copyright_notices"] as? [[String: Any]])
            var matches = try XCTUnwrap(notices[1]["staydown_matches"] as? [[String: Any]])
            var placement = try XCTUnwrap(matches[0]["image_placement"] as? [String: Any])
            placement.removeValue(forKey: key)
            matches[0]["image_placement"] = placement
            notices[1]["staydown_matches"] = matches
            missing["copyright_notices"] = notices
            XCTAssertThrowsError(try decode(CopyrightStaffQueueResponse.self, missing), key)
        }
    }

    private func encodedObject(_ value: some Encodable) throws -> [String: Any] {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        return try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(value)) as? [String: Any])
    }

    private func fixture(_ id: String) throws -> [String: Any] {
        try XCTUnwrap(JSONSerialization.jsonObject(with: ApiFixtureLoader.data(id)) as? [String: Any])
    }

    private func decode<T: Decodable>(_ type: T.Type, _ body: [String: Any]) throws -> T {
        try makeVouchaDecoder().decode(type, from: JSONSerialization.data(withJSONObject: body))
    }
}
