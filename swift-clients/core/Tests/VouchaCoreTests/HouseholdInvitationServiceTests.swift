import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class HouseholdInvitationServiceTests: XCTestCase {
    override func tearDown() {
        InvitationURLProtocol.reset()
        super.tearDown()
    }

    func testSixActionsUseExplicitProducerContractShapesAndForwardPagination() async throws {
        InvitationURLProtocol.reset()
        InvitationURLProtocol.requestHandlers = try [
            "GET \(sentPath)": (fixture("sent"), 200),
            "GET \(receivedPath)": (fixture("received"), 200),
            "POST \(sentPath)": (fixture("create"), 201),
            "DELETE \(sentPath)/invitation": (Data(), 204),
            "POST \(receivedPath)/invitation/acceptance": (fixture("accept"), 200),
            "POST \(receivedPath)/invitation/decline": (Data(), 204)
        ]
        let service = try makeService()
        let sent = try await service.sent(householdId: "home", after: "sent+/=", limit: 2)
        let received = try await service.received(after: "received+/=", limit: 3)
        let created = try await service.invite(
            householdId: "home",
            username: "household-invitee",
            relationship: "partner"
        )
        try await service.revoke(householdId: "home", invitationId: "invitation")
        let membership = try await service.accept(invitationId: "invitation")
        try await service.decline(invitationId: "invitation")

        XCTAssertEqual(sent.results.first?.id, created.id)
        XCTAssertEqual(received.results.first?.id, created.id)
        XCTAssertEqual(membership.individual.username, created.invitedUser.username)
        let urls = InvitationURLProtocol.capturedURLs
        XCTAssertEqual(urls.map(\.path), [
            sentPath, receivedPath, sentPath, "\(sentPath)/invitation",
            "\(receivedPath)/invitation/acceptance", "\(receivedPath)/invitation/decline"
        ])
        XCTAssertEqual(InvitationURLProtocol.capturedMethods, ["GET", "GET", "POST", "DELETE", "POST", "POST"])
        XCTAssertEqual(query(urls[0]), ["after": "sent+/=", "limit": "2"])
        XCTAssertEqual(query(urls[1]), ["after": "received+/=", "limit": "3"])
        let body = try XCTUnwrap(InvitationURLProtocol.capturedBodies[2]?.data(using: .utf8))
        XCTAssertEqual(try JSONSerialization.jsonObject(with: body) as? [String: String], [
            "user": "household-invitee", "relationship": "partner"
        ])
    }

    func testEveryActionPreservesForbiddenAndNotFoundResponses() async throws {
        let service = try makeService()
        let operations: [(String, () async throws -> Void)] = [
            ("GET \(sentPath)", {
                _ = try await service.sent(householdId: "home", after: nil, limit: 10)
            }),
            ("GET \(receivedPath)", {
                _ = try await service.received(after: nil, limit: 10)
            }),
            ("POST \(sentPath)", {
                _ = try await service.invite(householdId: "home", username: "unavailable", relationship: nil)
            }),
            ("DELETE \(sentPath)/invitation", {
                try await service.revoke(householdId: "home", invitationId: "invitation")
            }),
            ("POST \(receivedPath)/invitation/acceptance", {
                _ = try await service.accept(invitationId: "invitation")
            }),
            ("POST \(receivedPath)/invitation/decline", {
                try await service.decline(invitationId: "invitation")
            })
        ]
        for (request, operation) in operations {
            for status in [403, 404] {
                InvitationURLProtocol.reset()
                InvitationURLProtocol.requestHandlers = [request: (Data("{}".utf8), status)]
                do {
                    try await operation()
                    XCTFail("Expected \(status) for \(request)")
                } catch VouchaError.forbidden {
                    XCTAssertEqual(status, 403, request)
                } catch VouchaError.notFound {
                    XCTAssertEqual(status, 404, request)
                } catch {
                    XCTFail("Unexpected error for \(request): \(error)")
                }
            }
        }
    }

    private let sentPath = "/api/v1/households/home/invitations"
    private let receivedPath = "/api/v1/my/household-invitations"

    private func makeService() throws -> HouseholdInvitationService {
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999"))),
            cookieStorage: IsolatedHTTPCookieStorage.make(), protocolClasses: [InvitationURLProtocol.self]
        )
        return HouseholdInvitationService(client: client)
    }

    /// Explicit pinned producer contract checks; these web fixtures are not native registry admissions.
    private func fixture(_ kind: String) throws -> Data {
        try Data(contentsOf: ApiFixtureLoader.fixtureURL("responses/web.household-invitations.\(kind).json"))
    }

    private func query(_ url: URL) -> [String: String] {
        Dictionary(uniqueKeysWithValues: (URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems ?? [])
            .map { ($0.name, $0.value ?? "") })
    }
}
