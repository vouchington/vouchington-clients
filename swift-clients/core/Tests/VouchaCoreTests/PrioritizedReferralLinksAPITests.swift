import Foundation
@testable import VouchaAPI
import VouchaCore
import VouchaModels
import XCTest

final class PrioritizedReferralLinksAPITests: XCTestCase {
    override func tearDown() {
        CapturingURLProtocol.responseData = Data("{}".utf8)
        CapturingURLProtocol.responseStatusCode = 200
        CapturingURLProtocol.lastRequestURL = nil
        super.tearDown()
    }

    func testPrioritizedEndpointPreservesOfficialFlagsPriorityGroupsAndServerOrder() async throws {
        var payload = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.referral-links.prioritized.default")
        ) as? [String: Any])
        let links = try XCTUnwrap(payload["links"] as? [[String: Any]])
        var official = try XCTUnwrap(links.first)
        official["id"] = "official-link"
        official["user_id"] = NSNull()
        official["is_official"] = true
        official["priority_group"] = 5
        var everyoneElse = try XCTUnwrap(links.first)
        everyoneElse["id"] = "everyone-else-link"
        everyoneElse["is_official"] = false
        everyoneElse["priority_group"] = 6
        // Preserve the server's ordering even when it differs from numeric priority order.
        payload["links"] = [everyoneElse, official]
        CapturingURLProtocol.responseData = try JSONSerialization.data(withJSONObject: payload)
        CapturingURLProtocol.responseStatusCode = 200
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [CapturingURLProtocol.self]
        )

        let response: PrioritizedReferralLinksResponse = try await client.send(
            .prioritizedReferralLinks(referralProgramId: "test-program", all: true)
        )

        XCTAssertEqual(
            CapturingURLProtocol.lastRequestURL?.path,
            "/api/v1/topics/test-program/prioritized-referral-links"
        )
        XCTAssertEqual(CapturingURLProtocol.lastRequestURL?.query, "all=true")
        XCTAssertEqual(response.links.map(\.id), ["everyone-else-link", "official-link"])
        XCTAssertEqual(response.links.map(\.priorityGroup), [6, 5])
        XCTAssertEqual(response.links.map(\.isOfficial), [false, true])
        XCTAssertEqual(response.links.first?.userId, "user-1")
        XCTAssertNil(response.links.last?.userId)
        XCTAssertEqual(response.users["user-1"]?.username, "testuser")
    }
}
