import Foundation
import VouchaModels
import XCTest

final class OAuthGrantSessionMetadataTests: XCTestCase {
    func testRequiredNullableSessionMetadataRoundTrips() throws {
        for values in [
            ["attestation_status": NSNull(), "session_id": NSNull()],
            ["attestation_status": "verified", "session_id": "session-1"]
        ] as [[String: Any]] {
            let grant = try decode(values)
            XCTAssertEqual(grant.attestationStatus, values["attestation_status"] as? String)
            XCTAssertEqual(grant.sessionId, values["session_id"] as? String)
            let encoder = JSONEncoder()
            encoder.keyEncodingStrategy = .convertToSnakeCase
            encoder.dateEncodingStrategy = .iso8601
            let json = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(grant)) as? [String: Any])
            XCTAssertTrue(json["last_used_at"] is NSNull)
            for key in ["attestation_status", "session_id"] {
                if let value = values[key] as? String {
                    XCTAssertEqual(json[key] as? String, value)
                } else {
                    XCTAssertTrue(json[key] is NSNull)
                }
            }
        }
    }

    func testMissingAndNonStringMetadataAreRejected() {
        for values: [String: Any] in [
            [:], ["attestation_status": NSNull()], ["session_id": NSNull()],
            ["attestation_status": 1, "session_id": NSNull()],
            ["attestation_status": NSNull(), "session_id": false]
        ] {
            XCTAssertThrowsError(try decode(values))
        }
    }

    func testLastUsedAtIsRequiredAndMustBeADateOrNull() throws {
        let metadata: [String: Any] = ["attestation_status": NSNull(), "session_id": NSNull()]
        let grant = try decode(metadata)
        XCTAssertNil(grant.lastUsedAt)
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.dateEncodingStrategy = .iso8601
        var json = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(grant)) as? [String: Any])
        json.removeValue(forKey: "last_used_at")
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            OAuthGrant.self,
            from: JSONSerialization.data(withJSONObject: json)
        ))
        json["last_used_at"] = false
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            OAuthGrant.self,
            from: JSONSerialization.data(withJSONObject: json)
        ))
    }

    private func decode(_ metadata: [String: Any]) throws -> OAuthGrant {
        var object: [String: Any] = [
            "id": "grant-1", "client": [
                "id": "client-1",
                "client_id": "app-1",
                "client_name": "Agent",
                "verified": true
            ],
            "resource": "https://voucha.ai/api/v1/mcp", "scopes": ["mcp.user:read"],
            "consented_at": "2026-09-01T12:00:00Z", "last_used_at": NSNull()
        ]
        object.merge(metadata) { _, value in value }
        return try makeVouchaDecoder().decode(OAuthGrant.self, from: JSONSerialization.data(withJSONObject: object))
    }
}
