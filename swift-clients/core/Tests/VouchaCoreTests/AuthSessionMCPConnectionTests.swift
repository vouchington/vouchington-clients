import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class AuthSessionMCPConnectionTests: XCTestCase {
    func testCurrentSessionFixtureRetainsEmptyAndPopulatedConnections() throws {
        let sessions = try makeVouchaDecoder().decode(
            Page<AuthSession>.self,
            from: ApiFixtureLoader.data("native.auth.sessions.default")
        )
        XCTAssertEqual(sessions.results.first?.mcpConnections.count, 0)
        let connection = try XCTUnwrap(sessions.results.last?.mcpConnections.first)
        XCTAssertEqual(connection.grantId, "00000000-0000-7000-8000-000000000711")
        XCTAssertEqual(connection.clientName, "Voucha iOS")
        XCTAssertEqual(connection.attestationStatus, "attested")
        XCTAssertEqual(connection.lastMintedAt, "2026-07-06T11:45:00.000Z")
        let encoded = try APIClient.makeEncoder().encode(sessions)
        let decoded = try makeVouchaDecoder().decode(Page<AuthSession>.self, from: encoded)
        XCTAssertTrue(try XCTUnwrap(decoded.results.first).mcpConnections.isEmpty)
        XCTAssertEqual(decoded.results.last?.mcpConnections.first?.lastMintedAt, connection.lastMintedAt)
    }

    func testConnectionArrayAndAllConnectionStringsAreRequired() throws {
        let data = try ApiFixtureLoader.data("native.auth.sessions.default")
        let root = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let rows = try XCTUnwrap(root["results"] as? [[String: Any]])
        var session = try XCTUnwrap(rows.first)
        session.removeValue(forKey: "mcp_connections")
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            AuthSession.self,
            from: JSONSerialization.data(withJSONObject: session)
        ))
        session["mcp_connections"] = NSNull()
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            AuthSession.self,
            from: JSONSerialization.data(withJSONObject: session)
        ))
        session["mcp_connections"] = [NSNull()]
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            AuthSession.self,
            from: JSONSerialization.data(withJSONObject: session)
        ))
        let connection = [
            "grant_id": "g",
            "client_name": "Client",
            "attestation_status": "attested",
            "last_minted_at": "time"
        ]
        for key in connection.keys {
            var missing: [String: Any] = connection
            missing.removeValue(forKey: key)
            XCTAssertThrowsError(try makeVouchaDecoder().decode(
                AuthSessionMCPConnection.self,
                from: JSONSerialization.data(withJSONObject: missing)
            ))
            missing[key] = NSNull()
            XCTAssertThrowsError(try makeVouchaDecoder().decode(
                AuthSessionMCPConnection.self,
                from: JSONSerialization.data(withJSONObject: missing)
            ))
        }
    }
}
