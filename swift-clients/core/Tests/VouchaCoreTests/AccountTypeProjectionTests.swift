import Foundation
@testable import VouchaModels
import XCTest

final class AccountTypeProjectionTests: XCTestCase {
    func testStagedIdentityDecodesEveryAccountClassificationInPublicAndPrivateProjections() throws {
        let envelope = try XCTUnwrap(
            JSONSerialization.jsonObject(with: ApiFixtureLoader.data("swift.my.identity.default"))
                as? [String: Any]
        )
        let original = try XCTUnwrap(envelope["identity"] as? [String: Any])
        XCTAssertNil(original["is_official_account"])
        XCTAssertNil(original["is_agent"])

        let cases: [AccountType?] = [.official, .system, .aiAgent, nil]
        for accountType in cases {
            var identity = original
            identity["account_type"] = accountType?.rawValue as Any? ?? NSNull()
            let data = try JSONSerialization.data(withJSONObject: identity)
            let decoder = JSONDecoder()
            decoder.keyDecodingStrategy = .convertFromSnakeCase

            let publicUser = try decoder.decode(PublicUser.self, from: data)
            let privateUser = try decoder.decode(PrivateUser.self, from: data)

            XCTAssertEqual(publicUser.accountType, accountType)
            XCTAssertEqual(privateUser.accountType, accountType)
            XCTAssertEqual(publicUser.id, privateUser.id)
        }
    }

    func testStagedCommunityAndReferralUserProjectionsDecodeEveryClassification() throws {
        let cases: [AccountType?] = [.official, .system, .aiAgent, nil]
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        for accountType in cases {
            let community = try stagedUser(
                "entity-provenance.communities", accountType: accountType
            )
            let owner = try decoder.decode(CommunityOwner.self, from: community)
            XCTAssertEqual(owner.accountType, accountType)

            let click = try stagedUser(
                "native.referral-clicks.mine.default", accountType: accountType
            )
            let clickUser = try decoder.decode(ReferralClickLogUser.self, from: click)
            XCTAssertEqual(clickUser.accountType, accountType)

            let feed = try stagedUser(
                "web.referral-links.feed.default", accountType: accountType
            )
            let feedUser = try decoder.decode(ReferralLinkUser.self, from: feed)
            XCTAssertEqual(feedUser.accountType, accountType)
        }
    }

    private func stagedUser(_ fixture: String, accountType: AccountType?) throws -> Data {
        let response = try XCTUnwrap(
            JSONSerialization.jsonObject(with: ApiFixtureLoader.data(fixture)) as? [String: Any]
        )
        let users = try XCTUnwrap(response["users"] as? [String: [String: Any]])
        let user = try XCTUnwrap(users.sorted(by: { $0.key < $1.key }).first?.value)
        var projection = user
        projection["account_type"] = accountType?.rawValue as Any? ?? NSNull()
        return try JSONSerialization.data(withJSONObject: projection)
    }
}
