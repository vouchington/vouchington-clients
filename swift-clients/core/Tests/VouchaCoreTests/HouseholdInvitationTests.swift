import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class HouseholdInvitationTests: XCTestCase {
    func testExplicitProducerContractShapesDecodeEmbeddedUsersAndAcceptance() throws {
        let decoder = makeVouchaDecoder()
        for kind in ["sent", "received"] {
            let page = try decoder.decode(HouseholdInvitationPage.self, from: fixture(kind))
            let invitation = try XCTUnwrap(page.results.first)
            XCTAssertEqual(invitation.invitedUser.username, "household-invitee")
            XCTAssertEqual(invitation.invitedBy.username, "household-owner")
            XCTAssertEqual(invitation.householdOwner.username, "household-owner")
            XCTAssertEqual(invitation.relationship, "partner")
            XCTAssertNil(invitation.acceptedAt)
            XCTAssertNil(invitation.declinedAt)
            XCTAssertNil(invitation.revokedAt)
            XCTAssertFalse(page.pageInfo.hasNextPage)
        }
        let created = try decoder.decode(HouseholdInvitationResponse.self, from: fixture("create"))
        let accepted = try decoder.decode(HouseholdInvitationAcceptanceResponse.self, from: fixture("accept"))
        XCTAssertEqual(created.invitation.householdId, accepted.membership.householdId)
        XCTAssertEqual(accepted.membership.individual.username, created.invitation.invitedUser.username)
        XCTAssertEqual(accepted.membership.relationship, created.invitation.relationship)
    }

    func testNullableEmbeddedUsernamesAndRelationshipRemainAbsent() throws {
        let original = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture("create")) as? [String: Any])
        var invitation = try XCTUnwrap(original["invitation"] as? [String: Any])
        for key in ["invited_user", "invited_by", "household_owner"] {
            var user = try XCTUnwrap(invitation[key] as? [String: Any])
            user["username"] = NSNull()
            invitation[key] = user
        }
        invitation["relationship"] = NSNull()
        let data = try JSONSerialization.data(withJSONObject: ["invitation": invitation])
        let decoded = try makeVouchaDecoder().decode(HouseholdInvitationResponse.self, from: data).invitation
        XCTAssertNil(decoded.invitedUser.username)
        XCTAssertNil(decoded.invitedBy.username)
        XCTAssertNil(decoded.householdOwner.username)
        XCTAssertNil(decoded.relationship)
    }

    func testRequiredNullableInvitationKeysRejectMissingFields() throws {
        let original = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture("create")) as? [String: Any])
        let originalInvitation = try XCTUnwrap(original["invitation"] as? [String: Any])
        for (key, decodedKey) in [
            ("relationship", "relationship"), ("accepted_at", "acceptedAt"),
            ("declined_at", "declinedAt"), ("revoked_at", "revokedAt")
        ] {
            var invitation = originalInvitation
            invitation.removeValue(forKey: key)
            try assertMissingKeyRejected(invitation: invitation, key: decodedKey)
        }
        for key in ["invited_user", "invited_by", "household_owner"] {
            var invitation = originalInvitation
            var user = try XCTUnwrap(invitation[key] as? [String: Any])
            user.removeValue(forKey: "username")
            invitation[key] = user
            try assertMissingKeyRejected(invitation: invitation, key: "username")
        }
    }

    private func assertMissingKeyRejected(invitation: [String: Any], key: String) throws {
        let data = try JSONSerialization.data(withJSONObject: ["invitation": invitation])
        XCTAssertThrowsError(try makeVouchaDecoder().decode(HouseholdInvitationResponse.self, from: data)) { error in
            guard case let DecodingError.keyNotFound(missing, _) = error else {
                return XCTFail("Expected required nullable key rejection, received \(error)")
            }
            let snakeCase = key.split(separator: "_")
            let decodedKey = String(snakeCase[0]) + snakeCase.dropFirst().map(\.capitalized).joined()
            XCTAssertEqual(missing.stringValue, decodedKey)
        }
    }

    func testEndpointsEscapeIdentifiersForwardCursorsAndEncodeUsername() {
        let sent = Endpoint.householdInvitations(householdId: "home/one", after: "opaque+/=", limit: 12)
        assertEndpoint(sent, path: "/api/v1/households/home%2Fone/invitations")
        XCTAssertEqual(sent.queryItems, [
            URLQueryItem(name: "limit", value: "12"), URLQueryItem(name: "after", value: "opaque+/=")
        ])
        let received = Endpoint.receivedHouseholdInvitations(after: "next", limit: 2)
        assertEndpoint(received, path: "/api/v1/my/household-invitations")
        XCTAssertEqual(received.queryItems, [
            URLQueryItem(name: "limit", value: "2"), URLQueryItem(name: "after", value: "next")
        ])
        assertEndpoint(
            .createHouseholdInvitation(householdId: "home", username: "alice", relationship: "partner"),
            method: .POST, path: "/api/v1/households/home/invitations",
            body: ["user": "alice", "relationship": "partner"]
        )
        assertEndpoint(
            .createHouseholdInvitation(householdId: "home", username: "alice"),
            method: .POST, path: "/api/v1/households/home/invitations", body: ["user": "alice"]
        )
        assertEndpoint(
            .revokeHouseholdInvitation(householdId: "home/one", invitationId: "invite/two"),
            method: .DELETE, path: "/api/v1/households/home%2Fone/invitations/invite%2Ftwo"
        )
        assertEndpoint(
            .acceptHouseholdInvitation(invitationId: "invite/two"), method: .POST,
            path: "/api/v1/my/household-invitations/invite%2Ftwo/acceptance", body: [:]
        )
        assertEndpoint(
            .declineHouseholdInvitation(invitationId: "invite/two"), method: .POST,
            path: "/api/v1/my/household-invitations/invite%2Ftwo/decline", body: [:]
        )
    }

    private func fixture(_ kind: String) throws -> Data {
        // Producer currently declares these shared API examples for web only.
        // Read their canonical bodies without claiming native fixture registry ownership.
        try Data(contentsOf: ApiFixtureLoader.fixtureURL("responses/web.household-invitations.\(kind).json"))
    }
}
