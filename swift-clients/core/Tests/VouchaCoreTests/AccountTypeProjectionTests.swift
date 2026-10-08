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
}
