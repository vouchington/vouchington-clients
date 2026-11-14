import Foundation
import ViewInspector
@testable import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class HouseholdInvitationNotificationTests: NativeRouteSurfaceViewModelTestCase {
    private func notification() throws -> VouchaNotification {
        let original = ApiFixtureLoader.data("swift.notifications.default")
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: original) as? [String: Any])
        let notifications = try XCTUnwrap(object["notifications"] as? [String: [String: Any]])
        var value = try XCTUnwrap(notifications.values.first)
        value["entity_type"] = "household_invitation"
        value["target_entity"] = ["__entity_type": "household", "id": "private-household-id"]
        value["target_path"] = NSNull()
        value["target_intent"] = NSNull()
        value["read_at"] = "2026-01-01T00:00:00.000Z"
        return try APIClient.makeDecoder().decode(
            VouchaNotification.self,
            from: JSONSerialization.data(withJSONObject: value)
        )
    }

    private func client() -> APIClient {
        APIClient(
            config: AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    func testStructuredHouseholdNotificationActivationRendersReceivedListAtDestination() async throws {
        let api = client()
        let model = NotificationsListViewModel(client: api)
        let target = try await model.activate(notification: notification())
        XCTAssertEqual(target, "/my/household")
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: XCTUnwrap(target)))
        let surface = NativeRouteDestinationView(
            entry: route.entry,
            client: api,
            routeMatch: route.match,
            currentUserId: "invitee"
        )
        let invitations = try surface.inspect().find(HouseholdInvitationsSurface.self).actualView()
        XCTAssertNil(invitations.viewModel.householdId)
        XCTAssertNoThrow(try invitations.inspect().find(text: "Household invitations"))
        XCTAssertThrowsError(try invitations.inspect().find(ViewType.TextField.self))
    }

    func testHouseholdRouteOffersSentControlsOnlyForOwnedHousehold() throws {
        let surface = HouseholdSurface(client: client())
        surface.viewModel.memberHouseholdPagination = CursorPaginationState(items: [makeHousehold(
            id: "member-only",
            ownerId: "other"
        )])
        let receivedOnly = try surface.inspect().findAll(HouseholdInvitationsSurface.self)
        XCTAssertEqual(receivedOnly.count, 1)
        XCTAssertNil(try receivedOnly[0].actualView().viewModel.householdId)
        surface.viewModel.ownedHousehold = makeHousehold(id: "owned", ownerId: "current-user")
        let ownedAndReceived = try surface.inspect().findAll(HouseholdInvitationsSurface.self)
        XCTAssertEqual(ownedAndReceived.count, 2)
        XCTAssertEqual(try ownedAndReceived[1].actualView().viewModel.householdId, "owned")
        XCTAssertNoThrow(try ownedAndReceived[1].find(button: "Send invitation"))
    }

    func testCanonicalNotificationRowsNavigateToReceivedInvitationSurface() async throws {
        let notification = try notification()
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.dateEncodingStrategy = .iso8601
        let encoded = try encoder.encode(notification)
        let value = try JSONSerialization.jsonObject(with: encoded)
        let response = try JSONSerialization.data(withJSONObject: [
            "results": [["id": notification.id]],
            "page_info": ["has_next_page": false, "end_cursor": NSNull()],
            "notifications": [notification.id: value]
        ])
        CannedFeedURLProtocol.handlers["/api/v1/my/notifications"] = (response, 200)
        defer { CannedFeedURLProtocol.handlers = [:] }
        let model = try NativeRouteSurfaceViewModel(entry: entry(for: .notifications), client: client())
        await model.load()
        var target: String?
        let surface = NativeListSurface(viewModel: model) { target = $0 }
        try surface.inspect().find(button: notification.title).tap()
        XCTAssertEqual(target, "/my/household")
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: XCTUnwrap(target)))
        let destination = NativeRouteDestinationView(entry: route.entry, client: client(), currentUserId: "invitee")
        XCTAssertNoThrow(try destination.inspect().find(text: "Household invitations"))
    }
}
