import Foundation
import SwiftUI
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

    func testMissingWithheldDeletedAndAnonymousParticipantsHidePlacement() throws {
        let ownerId = "00000000-0000-7000-8000-000000000001"
        let legacyImageId = "00000000-0000-7000-8000-000000000211"
        let fixture = try roster()
        let owner = fixture[0]
        let member = fixture[1]

        try assertParticipant(
            [owner],
            currentUserId: ownerId,
            handle: "@fixtureuser",
            initials: "FI",
            role: "Owner",
            showsRemove: false
        )
        var withheld = member
        withheld["profile_image_placement"] = NSNull()
        XCTAssertEqual(withheld["profile_image_id"] as? String, legacyImageId)
        try assertParticipant(
            [owner, withheld],
            currentUserId: ownerId,
            handle: "@alice",
            initials: "AL",
            role: "Member",
            showsRemove: true
        )
        var deleted = member
        deleted["username"] = NSNull()
        deleted["profile_image_id"] = NSNull()
        deleted["profile_image_placement"] = NSNull()
        try assertParticipant(
            [owner, deleted],
            currentUserId: ownerId,
            handle: "Member",
            initials: "00",
            role: "Member",
            showsRemove: true
        )
        var anonymous = deleted
        anonymous["user_id"] = NSNull()
        try assertParticipant(
            [owner, anonymous],
            currentUserId: ownerId,
            handle: "Member",
            initials: "00",
            role: "Member",
            showsRemove: false
        )
    }

    private func assertParticipant(
        _ results: [[String: Any]],
        currentUserId: String,
        handle: String,
        initials: String,
        role: String,
        showsRemove: Bool
    ) throws {
        let page: [String: Any] = [
            "page_info": [
                "end_cursor": NSNull(),
                "has_next_page": false,
                "start_cursor": NSNull()
            ],
            "results": results
        ]
        let decoded = try APIClient.makeDecoder().decode(
            Page<ConversationParticipant>.self,
            from: JSONSerialization.data(withJSONObject: page)
        )
        let participant = try XCTUnwrap(decoded.results.last)
        let surface = try rosterSurface(decoded.results, currentUserId: currentUserId)
        let rendered = try surface.participantSection
            .environment(\.locale, Locale(identifier: "en"))
            .inspect()
        let avatar = try rendered.findAll(Avatar.self).last
        XCTAssertNil(participant.profileImagePlacement)
        XCTAssertNil(try XCTUnwrap(avatar).actualView().imageURL)
        XCTAssertTrue(try XCTUnwrap(avatar).accessibilityHidden())
        XCTAssertNoThrow(try rendered.find(text: initials))
        XCTAssertNoThrow(try rendered.find(text: handle))
        XCTAssertNoThrow(try rendered.find(text: role))
        let removeButtons = rendered.findAll(ViewType.Button.self).filter { button in
            (try? button.labelView().text().string()) == "Remove"
        }
        XCTAssertEqual(removeButtons.count, showsRemove ? 1 : 0)
    }

    private func roster() throws -> [[String: Any]] {
        let fixture = ApiFixtureLoader.data("native.messages.participants.default")
        let root = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture) as? [String: Any])
        return try XCTUnwrap(root["results"] as? [[String: Any]])
    }

    private func rosterSurface(
        _ participants: [ConversationParticipant],
        currentUserId: String
    ) throws -> DirectMessagesSurface {
        let apiBaseURL = try XCTUnwrap(URL(string: "http://localhost:2999"))
        let imageBaseURL = try XCTUnwrap(URL(string: "https://images.self-hosted.test"))
        let client = APIClient(
            config: AppConfig(baseURL: apiBaseURL, imageBaseURL: imageBaseURL),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let model = DirectMessagesViewModel(client: client, currentUserId: currentUserId)
        model.participants = participants
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/messages/conversation-1"))
        return DirectMessagesSurface(viewModel: model, routeMatch: route.match)
    }
}
