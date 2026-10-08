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

    func testPublicUserDecodesOrdinaryUsername() throws {
        let user: PublicUser = try decodeJSON(publicUserJSON(username: .present("alice")))

        XCTAssertEqual(user.username, "alice")
        let encoded = try encodedObject(user)
        XCTAssertEqual(encoded["username"] as? String, "alice")
    }

    func testPublicUserDecodesNullUsername() throws {
        let user: PublicUser = try decodeJSON(publicUserJSON(username: .null, displayAccountNull: true))

        XCTAssertNil(user.username)
        XCTAssertNil(user.displayAccount)
        XCTAssertNil(user.useDisplayNameFrom)
        let encoded = try encodedObject(user)
        XCTAssertNil(encoded["username"])
    }

    func testPublicUserDecodesOmittedUsername() throws {
        let user: PublicUser = try decodeJSON(publicUserJSON(username: .omitted))

        XCTAssertNil(user.username)
        let encoded = try encodedObject(user)
        XCTAssertNil(encoded["username"])
    }

    func testPrivateUserDecodesOrdinaryUsername() throws {
        let user: PrivateUser = try decodeJSON(privateUserJSON(username: .present("alice")))

        XCTAssertEqual(user.username, "alice")
        XCTAssertEqual(user.id, "user-1")
        let encoded = try encodedObject(user)
        XCTAssertEqual(encoded["username"] as? String, "alice")
    }

    func testPrivateUserDecodesNullUsername() throws {
        let user: PrivateUser = try decodeJSON(privateUserJSON(username: .null))

        XCTAssertNil(user.username)
        XCTAssertNil(user.useDisplayNameFrom)
        XCTAssertNil(user.emailAddress)
        XCTAssertNil(user.facebookAccount)
        XCTAssertEqual(user.xAccount?.id, "x-1")
        XCTAssertNil(user.xAccount?.name)
        XCTAssertNil(user.xAccount?.emailAddress)
        let encoded = try encodedObject(user)
        XCTAssertNil(encoded["username"])
    }

    func testPrivateUserDecodesOmittedUsername() throws {
        let user: PrivateUser = try decodeJSON(privateUserJSON(username: .omitted))

        XCTAssertNil(user.username)
        XCTAssertEqual(user.roles, ["member"])
        let encoded = try encodedObject(user)
        XCTAssertNil(encoded["username"])
    }

    private func decodeJSON<T: Decodable>(_ json: String) throws -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(T.self, from: Data(json.utf8))
    }

    private func encodedObject(_ value: some Encodable) throws -> [String: Any] {
        let encoded = try JSONEncoder().encode(value)
        return try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
    }

    private func publicUserJSON(username: UsernameFixture, displayAccountNull: Bool = false) -> String {
        var fields = ["\"id\": \"user-1\""]
        if let usernameField = username.jsonField {
            fields.append(usernameField)
        }
        if displayAccountNull {
            fields.append("\"display_account\": null")
            fields.append("\"use_display_name_from\": null")
        }
        return "{\n\(fields.joined(separator: ",\n"))\n}"
    }

    private func privateUserJSON(username: UsernameFixture) -> String {
        let usernameField = username.jsonField.map { "\($0),\n" } ?? ""
        return """
        {
          "__entity_type": "user",
          "id": "user-1",
          \(usernameField)  "roles": ["member"],
          "account_type": null,
          "use_display_name_from": null,
          "email_address": null,
          "individual_id": null,
          "phone_number": null,
          "facebook_account": null,
          "x_account": {
            "id": "x-1",
            "name": null,
            "email_address": null
          },
          "cards_visibility": "everyone",
          "rewards_program_statuses_visibility": "everyone",
          "spending_categories_visibility": "everyone",
          "follows_visibility": "everyone",
          "topic_follows_visibility": "everyone",
          "rss_feed_follows_visibility": "everyone",
          "community_memberships_visibility": "everyone",
          "followers_visibility": "everyone",
          "likes_visibility": "everyone",
          "direct_messages_audience": "users",
          "default_post_broadcast": "everyone",
          "default_post_privacy": "public",
          "is_engagement_emails_enabled": true,
          "news_digest_frequency": "weekly",
          "is_moderation_emails_enabled": true,
          "community_digest_frequency": "weekly",
          "moderation_email_cadence": "daily",
          "moderation_email_days_of_week": [1, 2, 3, 4, 5],
          "moderation_email_time_of_day": "09:00"
        }
        """
    }
}

private enum UsernameFixture {
    case present(String)
    case null
    case omitted

    var jsonField: String? {
        switch self {
        case let .present(value):
            "\"username\": \"\(value)\""
        case .null:
            "\"username\": null"
        case .omitted:
            nil
        }
    }
}
