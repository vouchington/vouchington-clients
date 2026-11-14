import Foundation
@testable import VouchaAPI
import VouchaModels
import VouchaTestSupport
import XCTest

final class CopyrightMCPResultFixtureTests: XCTestCase {
    private func structuredContent(_ id: String) throws -> [String: Any] {
        let root = try ContractRoot.url(requiredPaths: ["api-fixtures/v1/mcp-results.json"])
        let url = try ApiFixtureLoader.fixtureURL("mcp-results.json", root: root)
        let catalog = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any])
        XCTAssertEqual(catalog["version"] as? Int, 1)
        let cases = try XCTUnwrap(catalog["cases"] as? [[String: Any]])
        let selected: [String: Any] = try XCTUnwrap(cases.first(where: { $0["id"] as? String == id }))
        return try XCTUnwrap(selected["structuredContent"] as? [String: Any])
    }

    private func decode<T: Decodable>(_ type: T.Type, _ body: [String: Any]) throws -> T {
        try makeVouchaDecoder().decode(type, from: JSONSerialization.data(withJSONObject: body))
    }

    func testAssociationSearchResultDecodesWithOptionalWorkDescription() throws {
        var body = try structuredContent("native.mcp.search-copyright-notices-for-association.results")
        let decoded = try decode(CopyrightAssociationTargetsResponse.self, body)
        let notice = try XCTUnwrap(decoded.copyrightNotices.first)
        XCTAssertEqual(notice.id, "0198ffff-0000-7000-8000-000000000031")
        XCTAssertEqual(notice.claimantDisplayName, "Example Claimant")
        XCTAssertEqual(notice.workDescription, "Example work")
        XCTAssertFalse(decoded.pageInfo.hasNextPage)
        XCTAssertNil(decoded.pageInfo.endCursor)

        var notices = try XCTUnwrap(body["copyright_notices"] as? [[String: Any]])
        notices[0]["work_description"] = NSNull()
        body["copyright_notices"] = notices
        let absent = try decode(CopyrightAssociationTargetsResponse.self, body)
        XCTAssertNil(try XCTUnwrap(absent.copyrightNotices.first).workDescription)
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let encoded = try XCTUnwrap(
            JSONSerialization.jsonObject(with: encoder.encode(absent)) as? [String: Any]
        )
        let encodedNotices = try XCTUnwrap(encoded["copyright_notices"] as? [[String: Any]])
        XCTAssertTrue(encodedNotices[0]["work_description"] is NSNull)

        notices[0].removeValue(forKey: "work_description")
        body["copyright_notices"] = notices
        XCTAssertThrowsError(try decode(CopyrightAssociationTargetsResponse.self, body))
    }

    func testRepeatInfringerAccountsResultDecodesUsername() throws {
        let body = try structuredContent("native.mcp.list-repeat-infringer-accounts.accounts")
        let decoded = try decode(CopyrightRepeatInfringerAccountsResponse.self, body)
        let account = try XCTUnwrap(decoded.copyrightRepeatInfringerAccounts.first)
        XCTAssertEqual(account.accountUsername, "example-account")
        XCTAssertEqual(account.accountUserId, "0198ffff-0000-7000-8000-000000000032")
        XCTAssertEqual(account.incidentId, "0198ffff-0000-7000-8000-000000000033")
        XCTAssertTrue(account.isOperative)
        XCTAssertEqual(account.openReviewId, "0198ffff-0000-7000-8000-000000000034")
        XCTAssertFalse(account.terminationInEffect)
    }
}
