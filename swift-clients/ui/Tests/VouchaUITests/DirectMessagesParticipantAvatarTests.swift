import Foundation
import ViewInspector
@testable import VouchaAPI
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class DirectMessagesParticipantAvatarTests: NativeRouteSurfaceViewModelTestCase {
    func testRosterUsesCanonicalPlacementAndKeepsRoleWhenPlacementClears() throws {
        let fixture = ApiFixtureLoader.data("native.messages.participants.default")
        let response = try APIClient.makeDecoder().decode(Page<ConversationParticipant>.self, from: fixture)
        let apiBaseURL = try XCTUnwrap(URL(string: "http://localhost:2999"))
        let imageBaseURL = try XCTUnwrap(URL(string: "https://images.self-hosted.test"))
        let client = APIClient(
            config: AppConfig(baseURL: apiBaseURL, imageBaseURL: imageBaseURL),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let model = DirectMessagesViewModel(client: client, currentUserId: "fixtureuser")
        model.participants = response.results
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/conversation-1"))
        let surface = DirectMessagesSurface(viewModel: model, routeMatch: route.match)
        let avatars = try surface.participantSection.inspect().findAll(Avatar.self)
        XCTAssertEqual(avatars.count, 2)
        XCTAssertNil(try avatars[0].actualView().imageURL)
        XCTAssertEqual(
            try avatars[1].actualView().imageURL,
            "https://images.self-hosted.test/images/placements/00000000-0000-7000-8000-000000000311/3/00000000-0000-7000-8000-000000000211?w=96"
        )
        XCTAssertNoThrow(try surface.participantSection.inspect().find(text: "@alice"))
        XCTAssertNoThrow(try surface.participantSection.inspect().find(text: "Member"))

        var legacy = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture) as? [String: Any])
        var participants = try XCTUnwrap(legacy["results"] as? [[String: Any]])
        participants[1].removeValue(forKey: "profile_image_placement")
        legacy["results"] = participants
        model.participants = try APIClient.makeDecoder().decode(
            Page<ConversationParticipant>.self,
            from: JSONSerialization.data(withJSONObject: legacy)
        ).results
        let cleared = try surface.participantSection.inspect().findAll(Avatar.self)
        XCTAssertNil(try cleared[1].actualView().imageURL)
        XCTAssertNoThrow(try surface.participantSection.inspect().find(text: "@alice"))
    }
}
