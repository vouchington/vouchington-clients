import Foundation
@testable import VouchaModels
import XCTest

final class UserDecodingTests: XCTestCase {
    func testPublicUserDisplayAccountNameCanBeNull() throws {
        let user: PublicUser = try decodeJSON(
            """
            {
              "id": "user-1",
              "username": "alice",
              "display_account": {
                "id": "account-1",
                "name": null
              }
            }
            """
        )

        XCTAssertEqual(user.displayAccount?.id, "account-1")
        XCTAssertNil(user.displayAccount?.name)

        let encoded = try JSONEncoder().encode(user)
        let root = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        let displayAccount = try XCTUnwrap(root["display_account"] as? [String: Any])
        XCTAssertTrue(displayAccount["name"] is NSNull)
    }

    func testPublicUserDisplayAccountCanOmitProviderId() throws {
        let user: PublicUser = try decodeJSON(
            """
            {
              "id": "user-1",
              "username": "alice",
              "display_account": {
                "name": "Alice"
              }
            }
            """
        )

        XCTAssertNil(user.displayAccount?.id)
        XCTAssertEqual(user.displayAccount?.name, "Alice")
    }

    private func decodeJSON<T: Decodable>(_ json: String) throws -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(T.self, from: Data(json.utf8))
    }
}
