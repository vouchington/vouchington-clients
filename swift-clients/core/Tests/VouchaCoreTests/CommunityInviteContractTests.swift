import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class CommunityInviteContractTests: XCTestCase {
    func testListFixtureContainsMetadataWithoutRecoverableCode() throws {
        let data = ApiFixtureLoader.data("web.communities.invites.default")
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(CommunityInvitesResponse.self, from: data)
        let invite = try XCTUnwrap(response.communityInvites.values.first)
        XCTAssertEqual(invite.entityType, "community_invite")

        let encoded = try JSONEncoder().encode(invite)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        XCTAssertNil(object["code"])
        XCTAssertNil(object["code_hash"])
    }

    func testCreatedResponseCarriesCodeOnlyOnCreationType() throws {
        let data = Data("""
        {
          "community_invite": {
            "id": "invite-new",
            "community_id": "community-1",
            "code": "share-once",
            "invited_user_id": "user-2",
            "invited_email": null,
            "invited_by_id": "moderator-1",
            "accepted_at": null,
            "accepted_by_id": null,
            "declined_at": null,
            "revoked_at": null,
            "created_at": "2026-07-01T00:00:00Z",
            "__entity_type": "community_invite"
          }
        }
        """.utf8)
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(CreatedCommunityInviteResponse.self, from: data)
        XCTAssertEqual(response.communityInvite.code, "share-once")
        XCTAssertEqual(response.communityInvite.entityType, "community_invite")
    }
}
