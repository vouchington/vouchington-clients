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

        XCTAssertNil(user.displayAccount?.name)

        let encoded = try JSONEncoder().encode(user)
        let root = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        let displayAccount = try XCTUnwrap(root["display_account"] as? [String: Any])
        XCTAssertNil(displayAccount["id"])
        XCTAssertTrue(displayAccount["name"] is NSNull)
    }

    func testPublicUserDisplayAccountIsTheName() throws {
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

        XCTAssertEqual(user.displayAccount?.name, "Alice")

        let encoded = try JSONEncoder().encode(user)
        let root = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        let displayAccount = try XCTUnwrap(root["display_account"] as? [String: Any])
        XCTAssertNil(displayAccount["id"])
        XCTAssertEqual(displayAccount["name"] as? String, "Alice")
    }

    private func decodeJSON<T: Decodable>(_ json: String) throws -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(T.self, from: Data(json.utf8))
    }
}
